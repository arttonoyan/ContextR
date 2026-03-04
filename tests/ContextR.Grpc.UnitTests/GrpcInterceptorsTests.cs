using System.Text;
using ContextR.Transport.Grpc;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Grpc.UnitTests;

public sealed class GrpcInterceptorsTests
{
    [Fact]
    public async Task AsyncUnaryCall_InjectsMetadata_WhenContextIsPresent()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IContextWriter>()
            .SetContext(new TestContext { TenantId = "t1", UserId = "u1" });

        var interceptor = new ContextPropagationInterceptor<TestContext>(
            provider.GetRequiredService<IContextAccessor>(),
            provider.GetRequiredService<IContextPropagator<TestContext>>());

        Metadata? capturedHeaders = null;
        var call = interceptor.AsyncUnaryCall(
            new TestRequest(),
            CreateClientContext(),
            (request, context) =>
            {
                capturedHeaders = context.Options.Headers;
                return CreateUnaryCall(new TestResponse());
            });

        await call.ResponseAsync;

        Assert.NotNull(capturedHeaders);
        Assert.Equal("t1", capturedHeaders!.GetValue("x-tenant-id"));
        Assert.Equal("u1", capturedHeaders.GetValue("x-user-id"));
    }

    [Fact]
    public async Task AsyncUnaryCall_DoesNotInjectMetadata_WhenContextIsMissing()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider();

        var interceptor = new ContextPropagationInterceptor<TestContext>(
            provider.GetRequiredService<IContextAccessor>(),
            provider.GetRequiredService<IContextPropagator<TestContext>>());

        Metadata? capturedHeaders = null;
        var call = interceptor.AsyncUnaryCall(
            new TestRequest(),
            CreateClientContext(),
            (request, context) =>
            {
                capturedHeaders = context.Options.Headers;
                return CreateUnaryCall(new TestResponse());
            });

        await call.ResponseAsync;
        Assert.Null(capturedHeaders);
    }

    [Fact]
    public async Task AsyncUnaryCall_UsesDomain_WhenConfigured()
    {
        var services = BuildServicesWithDomain();
        using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new TestContext { TenantId = "default" });
        writer.SetContext("orders", new TestContext { TenantId = "orders" });

        var interceptor = new ContextPropagationInterceptor<TestContext>(
            accessor,
            provider.GetRequiredService<IContextPropagator<TestContext>>(),
            domain: "orders");

        Metadata? capturedHeaders = null;
        var call = interceptor.AsyncUnaryCall(
            new TestRequest(),
            CreateClientContext(),
            (request, context) =>
            {
                capturedHeaders = context.Options.Headers;
                return CreateUnaryCall(new TestResponse());
            });

        await call.ResponseAsync;
        Assert.Equal("orders", capturedHeaders!.GetValue("x-tenant-id"));
    }

    [Fact]
    public async Task UnaryServerHandler_ExtractsAndWritesContext()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider();

        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();
        var headers = propagator.CreateMetadata(new TestContext { TenantId = "server", UserId = "grpc" });
        var callContext = new TestServerCallContext(headers);

        var interceptor = new ContextInterceptor<TestContext>(
            provider.GetRequiredService<IContextWriter>(),
            propagator);

        await interceptor.UnaryServerHandler(
            new TestRequest(),
            callContext,
            static (request, context) => Task.FromResult(new TestResponse()));

        var stored = provider.GetRequiredService<IContextAccessor>().GetContext<TestContext>();
        Assert.NotNull(stored);
        Assert.Equal("server", stored!.TenantId);
        Assert.Equal("grpc", stored.UserId);
    }

    [Fact]
    public async Task UnaryServerHandler_WritesToDomain_WhenConfigured()
    {
        var services = BuildServicesWithDomain();
        using var provider = services.BuildServiceProvider();

        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();
        var headers = propagator.CreateMetadata(new TestContext { TenantId = "domain-value" });
        var callContext = new TestServerCallContext(headers);

        var interceptor = new ContextInterceptor<TestContext>(
            provider.GetRequiredService<IContextWriter>(),
            propagator,
            domain: "orders");

        await interceptor.UnaryServerHandler(
            new TestRequest(),
            callContext,
            static (request, context) => Task.FromResult(new TestResponse()));

        var accessor = provider.GetRequiredService<IContextAccessor>();
        Assert.Null(accessor.GetContext<TestContext>());
        Assert.Equal("domain-value", accessor.GetContext<TestContext>("orders")?.TenantId);
    }

    [Fact]
    public void BlockingUnaryCall_ClonesHeaders_AndPreservesBinaryEntries()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IContextWriter>()
            .SetContext(new TestContext { TenantId = "t1" });

        var interceptor = new ContextPropagationInterceptor<TestContext>(
            provider.GetRequiredService<IContextAccessor>(),
            provider.GetRequiredService<IContextPropagator<TestContext>>());

        var originalHeaders = new Metadata
        {
            { "existing", "value" },
            { "existing-bin", new byte[] { 1, 2, 3 } }
        };
        var context = new ClientInterceptorContext<TestRequest, TestResponse>(
            CreateMethod<TestRequest, TestResponse>(),
            "localhost",
            new CallOptions(headers: originalHeaders));

        Metadata? capturedHeaders = null;
        _ = interceptor.BlockingUnaryCall(
            new TestRequest(),
            context,
            (request, nextContext) =>
            {
                capturedHeaders = nextContext.Options.Headers;
                return new TestResponse();
            });

        Assert.NotNull(capturedHeaders);
        Assert.NotSame(originalHeaders, capturedHeaders);
        Assert.Equal("value", capturedHeaders!.GetValue("existing"));
        Assert.Equal([1, 2, 3], capturedHeaders.First(e => e.Key == "existing-bin").ValueBytes);
        Assert.Equal("t1", capturedHeaders.GetValue("x-tenant-id"));
    }

    [Fact]
    public void AsyncServerStreamingCall_InjectsContextMetadata()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IContextWriter>()
            .SetContext(new TestContext { TenantId = "stream" });

        var interceptor = new ContextPropagationInterceptor<TestContext>(
            provider.GetRequiredService<IContextAccessor>(),
            provider.GetRequiredService<IContextPropagator<TestContext>>());

        Metadata? capturedHeaders = null;
        _ = interceptor.AsyncServerStreamingCall(
            new TestRequest(),
            CreateClientContext(),
            (request, context) =>
            {
                capturedHeaders = context.Options.Headers;
                return CreateServerStreamingCall();
            });

        Assert.Equal("stream", capturedHeaders!.GetValue("x-tenant-id"));
    }

    [Fact]
    public void AsyncClientStreamingCall_InjectsContextMetadata()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IContextWriter>()
            .SetContext(new TestContext { TenantId = "client-stream" });

        var interceptor = new ContextPropagationInterceptor<TestContext>(
            provider.GetRequiredService<IContextAccessor>(),
            provider.GetRequiredService<IContextPropagator<TestContext>>());

        Metadata? capturedHeaders = null;
        _ = interceptor.AsyncClientStreamingCall(
            CreateClientContext(),
            context =>
            {
                capturedHeaders = context.Options.Headers;
                return CreateClientStreamingCall();
            });

        Assert.Equal("client-stream", capturedHeaders!.GetValue("x-tenant-id"));
    }

    [Fact]
    public void AsyncDuplexStreamingCall_InjectsContextMetadata()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IContextWriter>()
            .SetContext(new TestContext { TenantId = "duplex" });

        var interceptor = new ContextPropagationInterceptor<TestContext>(
            provider.GetRequiredService<IContextAccessor>(),
            provider.GetRequiredService<IContextPropagator<TestContext>>());

        Metadata? capturedHeaders = null;
        _ = interceptor.AsyncDuplexStreamingCall(
            CreateClientContext(),
            context =>
            {
                capturedHeaders = context.Options.Headers;
                return CreateDuplexStreamingCall();
            });

        Assert.Equal("duplex", capturedHeaders!.GetValue("x-tenant-id"));
    }

    [Fact]
    public async Task ServerStreaming_AndClientStreaming_AndDuplexHandlers_ExtractContext()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();
        var interceptor = new ContextInterceptor<TestContext>(
            provider.GetRequiredService<IContextWriter>(),
            propagator);
        var headers = propagator.CreateMetadata(new TestContext { TenantId = "grpc-server", UserId = "u1" });
        var callContext = new TestServerCallContext(headers);

        await interceptor.ServerStreamingServerHandler(
            new TestRequest(),
            new TestServerStreamWriter<TestResponse>(),
            callContext,
            static (request, writer, context) => Task.CompletedTask);
        await interceptor.ClientStreamingServerHandler(
            new TestAsyncStreamReader<TestRequest>(),
            callContext,
            static (stream, context) => Task.FromResult(new TestResponse()));
        await interceptor.DuplexStreamingServerHandler(
            new TestAsyncStreamReader<TestRequest>(),
            new TestServerStreamWriter<TestResponse>(),
            callContext,
            static (stream, writer, context) => Task.CompletedTask);

        var stored = provider.GetRequiredService<IContextAccessor>().GetContext<TestContext>();
        Assert.NotNull(stored);
        Assert.Equal("grpc-server", stored!.TenantId);
        Assert.Equal("u1", stored.UserId);
    }

    private static ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "x-tenant-id")
            .MapProperty(c => c.UserId, "x-user-id")));
        return services;
    }

    private static ServiceCollection BuildServicesWithDomain()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx =>
        {
            ctx.Add<TestContext>(reg => reg.MapProperty(c => c.TenantId, "x-tenant-id"));
            ctx.AddDomain("orders", domain =>
                domain.Add<TestContext>(reg => reg.MapProperty(c => c.TenantId, "x-tenant-id")));
        });

        return services;
    }

    private static ClientInterceptorContext<TestRequest, TestResponse> CreateClientContext()
        => new(
            CreateMethod<TestRequest, TestResponse>(),
            "localhost",
            new CallOptions());

    private static Method<TRequest, TResponse> CreateMethod<TRequest, TResponse>()
        where TRequest : class, new()
        where TResponse : class, new()
        => new(
            MethodType.Unary,
            "test.Service",
            "Unary",
            new Marshaller<TRequest>(
                static value => Encoding.UTF8.GetBytes("request"),
                static data => new TRequest()),
            new Marshaller<TResponse>(
                static value => Encoding.UTF8.GetBytes("response"),
                static data => new TResponse()));

    private static AsyncUnaryCall<TestResponse> CreateUnaryCall(TestResponse response)
        => new(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            static () => Status.DefaultSuccess,
            static () => new Metadata(),
            static () => { });

    private static AsyncServerStreamingCall<TestResponse> CreateServerStreamingCall()
        => new(
            new TestAsyncStreamReader<TestResponse>(),
            Task.FromResult(new Metadata()),
            static () => Status.DefaultSuccess,
            static () => new Metadata(),
            static () => { });

    private static AsyncClientStreamingCall<TestRequest, TestResponse> CreateClientStreamingCall()
        => new(
            new TestClientStreamWriter<TestRequest>(),
            Task.FromResult(new TestResponse()),
            Task.FromResult(new Metadata()),
            static () => Status.DefaultSuccess,
            static () => new Metadata(),
            static () => { });

    private static AsyncDuplexStreamingCall<TestRequest, TestResponse> CreateDuplexStreamingCall()
        => new(
            new TestClientStreamWriter<TestRequest>(),
            new TestAsyncStreamReader<TestResponse>(),
            Task.FromResult(new Metadata()),
            static () => Status.DefaultSuccess,
            static () => new Metadata(),
            static () => { });

    private sealed class TestRequest;

    private sealed class TestResponse;

    private sealed class TestContext
    {
        public string? TenantId { get; init; }
        public string? UserId { get; init; }
    }

    private sealed class TestServerCallContext : ServerCallContext
    {
        private readonly Metadata _requestHeaders;
        private readonly Metadata _responseTrailers = new();
        private Status _status;
        private WriteOptions? _writeOptions;

        public TestServerCallContext(Metadata requestHeaders)
        {
            _requestHeaders = requestHeaders;
        }

        protected override string MethodCore => "test.Service/Unary";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "ipv4:127.0.0.1:5000";
        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
        protected override Metadata RequestHeadersCore => _requestHeaders;
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => _responseTrailers;
        protected override Status StatusCore
        {
            get => _status;
            set => _status = value;
        }

        protected override WriteOptions? WriteOptionsCore
        {
            get => _writeOptions;
            set => _writeOptions = value;
        }

        protected override AuthContext AuthContextCore =>
            new("test", new Dictionary<string, List<AuthProperty>>());

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
            => throw new NotSupportedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
            => Task.CompletedTask;
    }

    private sealed class TestAsyncStreamReader<T> : IAsyncStreamReader<T>
    {
        public T Current => default!;
        public Task<bool> MoveNext(CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class TestClientStreamWriter<T> : IClientStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }
        public Task WriteAsync(T message) => Task.CompletedTask;
        public Task CompleteAsync() => Task.CompletedTask;
    }

    private sealed class TestServerStreamWriter<T> : IServerStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }
        public Task WriteAsync(T message) => Task.CompletedTask;
    }
}
