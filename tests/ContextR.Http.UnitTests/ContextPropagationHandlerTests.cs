using ContextR.Transport.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Http.UnitTests;

public sealed class ContextPropagationHandlerTests
{
    [Fact]
    public async Task SendAsync_InjectsContextHeaders_WhenContextIsPresent()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")
            .MapProperty(c => c.UserId, "X-User-Id")));

        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        writer.SetContext(new TestContext { TenantId = "t1", UserId = "u42" });

        var handler = new ContextPropagationHandler<TestContext>(accessor, propagator)
        {
            InnerHandler = new StubHandler()
        };

        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
        using var response = await client.SendAsync(request);

        Assert.True(request.Headers.Contains("X-Tenant-Id"));
        Assert.True(request.Headers.Contains("X-User-Id"));
        Assert.Equal("t1", request.Headers.GetValues("X-Tenant-Id").Single());
        Assert.Equal("u42", request.Headers.GetValues("X-User-Id").Single());
    }

    [Fact]
    public async Task SendAsync_DoesNotInjectHeaders_WhenContextIsNull()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")));

        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var handler = new ContextPropagationHandler<TestContext>(accessor, propagator)
        {
            InnerHandler = new StubHandler()
        };

        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
        using var response = await client.SendAsync(request);

        Assert.False(request.Headers.Contains("X-Tenant-Id"));
    }

    [Fact]
    public async Task SendAsync_CallsInnerHandler()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")));

        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var stub = new StubHandler();
        var handler = new ContextPropagationHandler<TestContext>(accessor, propagator)
        {
            InnerHandler = stub
        };

        using var client = new HttpClient(handler);
        using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost"));

        Assert.True(stub.WasCalled);
    }

    [Fact]
    public void UseGlobalHttpPropagation_RegistersHandler_InServiceCollection()
    {
        var services = new ServiceCollection();

        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")
            .UseGlobalHttpPropagation()));

        var handlerDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(ContextPropagationHandler<TestContext>));

        Assert.NotNull(handlerDescriptor);
    }

    [Fact]
    public void AddContextRHandler_RegistersHandler_PerClient()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")));

        services.AddHttpClient("test-client")
            .AddContextRHandler<TestContext>();

        var handlerDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(ContextPropagationHandler<TestContext>));

        Assert.NotNull(handlerDescriptor);
    }

    [Fact]
    public void UseGlobalHttpPropagation_WithDomain_RegistersFactoryDescriptor()
    {
        var services = new ServiceCollection();

        services.AddContextR(ctx =>
        {
            ctx.Add<TestContext>()
               .AddDomain("my-domain", d => d.Add<TestContext>(reg => reg
                   .MapProperty(c => c.TenantId, "X-Tenant-Id")
                   .UseGlobalHttpPropagation()));
        });

        var handlerDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(ContextPropagationHandler<TestContext>));

        Assert.NotNull(handlerDescriptor);
        Assert.NotNull(handlerDescriptor.ImplementationFactory);
    }

    [Fact]
    public void UseGlobalHttpPropagation_IsChainable()
    {
        var services = new ServiceCollection();
        IContextRegistrationBuilder<TestContext>? capturedBuilder = null;

        services.AddContextR(ctx => ctx.Add<TestContext>(reg =>
        {
            capturedBuilder = reg
                .MapProperty(c => c.TenantId, "X-Tenant-Id")
                .UseGlobalHttpPropagation();
        }));

        Assert.NotNull(capturedBuilder);
    }

    [Fact]
    public void AddContextRHandler_IsChainable()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")));

        var httpBuilder = services.AddHttpClient("test-client");
        var result = httpBuilder.AddContextRHandler<TestContext>();

        Assert.Same(httpBuilder, result);
    }

    [Fact]
    public async Task PublicConstructor_UsesDomainNull()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")));
        using var provider = services.BuildServiceProvider();

        var writer = provider.GetRequiredService<IContextWriter>();
        writer.SetContext(new TestContext { TenantId = "default-value" });

        var handler = new ContextPropagationHandler<TestContext>(
            provider.GetRequiredService<IContextAccessor>(),
            provider.GetRequiredService<IContextPropagator<TestContext>>())
        {
            InnerHandler = new StubHandler()
        };

        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost");
        await client.SendAsync(request);

        Assert.Equal("default-value", request.Headers.GetValues("X-Tenant-Id").Single());
    }

    public class TestContext
    {
        public string? TenantId { get; set; }
        public string? UserId { get; set; }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public bool WasCalled { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
