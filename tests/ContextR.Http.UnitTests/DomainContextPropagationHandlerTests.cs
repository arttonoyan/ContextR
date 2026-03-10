using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Http.UnitTests;

public sealed class DomainContextPropagationHandlerTests
{
    [Fact]
    public async Task SendAsync_ReadsFromDomain_WhenDomainIsSpecified()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx =>
        {
            ctx.Add<TestContext>()
               .AddDomain("orders", d => d.Add<TestContext>(reg => reg
                   .MapProperty(c => c.TenantId, "X-Tenant-Id")));
        });

        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        writer.SetContext("orders", new TestContext { TenantId = "t-orders" });

        var handler = new ContextPropagationHandler<TestContext>(accessor, propagator, domain: "orders")
        {
            InnerHandler = new StubHandler()
        };

        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
        using var response = await client.SendAsync(request);

        Assert.True(request.Headers.Contains("X-Tenant-Id"));
        Assert.Equal("t-orders", request.Headers.GetValues("X-Tenant-Id").Single());
    }

    [Fact]
    public async Task SendAsync_IgnoresDefaultDomain_WhenDomainIsSpecified()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx =>
        {
            ctx.Add<TestContext>(reg => reg
                .MapProperty(c => c.TenantId, "X-Tenant-Id"));
        });

        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        writer.SetContext(new TestContext { TenantId = "t-default" });

        var handler = new ContextPropagationHandler<TestContext>(accessor, propagator, domain: "orders")
        {
            InnerHandler = new StubHandler()
        };

        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
        using var response = await client.SendAsync(request);

        Assert.False(request.Headers.Contains("X-Tenant-Id"));
    }

    [Fact]
    public async Task SendAsync_ReadsFromDefault_WhenDomainIsNull()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")));

        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        writer.SetContext(new TestContext { TenantId = "t-default" });

        var handler = new ContextPropagationHandler<TestContext>(accessor, propagator, domain: null)
        {
            InnerHandler = new StubHandler()
        };

        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
        using var response = await client.SendAsync(request);

        Assert.True(request.Headers.Contains("X-Tenant-Id"));
        Assert.Equal("t-default", request.Headers.GetValues("X-Tenant-Id").Single());
    }

    public class TestContext
    {
        public string? TenantId { get; set; }
        public string? UserId { get; set; }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
