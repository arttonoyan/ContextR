using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContextR.Grpc.UnitTests;

public sealed class ContextRGrpcExtensionsTests
{
    [Fact]
    public void UseGlobalGrpcPropagation_IsChainable_AndRegistersInterceptorOptions()
    {
        var services = new ServiceCollection();
        IContextTypeBuilder<TestContext>? capturedBuilder = null;

        services.AddContextR(ctx => ctx.Add<TestContext>(reg =>
        {
            capturedBuilder = reg
                .MapProperty(c => c.TenantId, "x-tenant-id")
                .UseGlobalGrpcPropagation();
        }));

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<GrpcClientFactoryOptions>>();
        var options = optionsMonitor.Get(Options.DefaultName);

        Assert.NotNull(capturedBuilder);
        Assert.Contains(options.InterceptorRegistrations, static x => x.Scope == InterceptorScope.Client);
    }

    [Fact]
    public void AddContextRGrpcPropagation_IsChainable()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "x-tenant-id")));

        var uri = new Uri("http://localhost");
        var grpcBuilder = services.AddGrpcClient<TestGrpcClient>(options => options.Address = uri);
        var result = grpcBuilder.AddContextRGrpcPropagation<TestContext>();

        Assert.Same(grpcBuilder, result);
    }

    private sealed class TestContext
    {
        public string? TenantId { get; init; }
    }

    private sealed class TestGrpcClient;
}
