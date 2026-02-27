using ContextR.AspNetCore.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.AspNetCore.UnitTests;

public sealed class DomainContextMiddlewareTests
{
    [Fact]
    public async Task Middleware_WritesToDomain_WhenDomainIsSpecified()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx =>
        {
            ctx.Add<TestContext>()
               .AddDomain("orders", d => d.Add<TestContext>(reg => reg
                   .MapProperty(c => c.TenantId, "X-Tenant-Id")));
        });
        using var provider = services.BuildServiceProvider();

        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        TestContext? domainContext = null;
        TestContext? defaultContext = null;
        RequestDelegate next = _ =>
        {
            domainContext = accessor.GetContext<TestContext>("orders");
            defaultContext = accessor.GetContext<TestContext>();
            return Task.CompletedTask;
        };

        var middleware = new ContextMiddleware<TestContext>(next, domain: "orders");
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-Id"] = "t1";
        httpContext.RequestServices = provider;

        await middleware.InvokeAsync(httpContext, propagator, writer);

        Assert.NotNull(domainContext);
        Assert.Equal("t1", domainContext.TenantId);
        Assert.Null(defaultContext);
    }

    [Fact]
    public async Task Middleware_WritesToDefault_WhenDomainIsNull()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")));
        using var provider = services.BuildServiceProvider();

        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        TestContext? capturedContext = null;
        RequestDelegate next = _ =>
        {
            capturedContext = accessor.GetContext<TestContext>();
            return Task.CompletedTask;
        };

        var middleware = new ContextMiddleware<TestContext>(next, domain: null);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-Id"] = "t1";
        httpContext.RequestServices = provider;

        await middleware.InvokeAsync(httpContext, propagator, writer);

        Assert.NotNull(capturedContext);
        Assert.Equal("t1", capturedContext.TenantId);
    }

    [Fact]
    public async Task Middleware_DomainAware_DoesNotSetContext_WhenNoHeaders()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx =>
        {
            ctx.Add<TestContext>()
               .AddDomain("orders", d => d.Add<TestContext>(reg => reg
                   .MapProperty(c => c.TenantId, "X-Tenant-Id")));
        });
        using var provider = services.BuildServiceProvider();

        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        TestContext? domainContext = null;
        RequestDelegate next = _ =>
        {
            domainContext = accessor.GetContext<TestContext>("orders");
            return Task.CompletedTask;
        };

        var middleware = new ContextMiddleware<TestContext>(next, domain: "orders");
        var httpContext = new DefaultHttpContext { RequestServices = provider };

        await middleware.InvokeAsync(httpContext, propagator, writer);

        Assert.Null(domainContext);
    }

    public class TestContext
    {
        public string? TenantId { get; set; }
        public string? UserId { get; set; }
    }
}
