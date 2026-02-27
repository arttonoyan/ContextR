using ContextR.AspNetCore.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.AspNetCore.UnitTests;

public sealed class ContextMiddlewareTests
{
    [Fact]
    public async Task Middleware_ExtractsContext_FromRequestHeaders()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")
            .MapProperty(c => c.UserId, "X-User-Id")));
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

        var middleware = new ContextMiddleware<TestContext>(next);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-Id"] = "t1";
        httpContext.Request.Headers["X-User-Id"] = "u42";
        httpContext.RequestServices = provider;

        await middleware.InvokeAsync(httpContext, propagator, writer);

        Assert.NotNull(capturedContext);
        Assert.Equal("t1", capturedContext.TenantId);
        Assert.Equal("u42", capturedContext.UserId);
    }

    [Fact]
    public async Task Middleware_DoesNotSetContext_WhenNoHeadersPresent()
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

        var middleware = new ContextMiddleware<TestContext>(next);
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = provider;

        await middleware.InvokeAsync(httpContext, propagator, writer);

        Assert.Null(capturedContext);
    }

    [Fact]
    public async Task Middleware_CallsNext()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")));
        using var provider = services.BuildServiceProvider();

        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();
        var writer = provider.GetRequiredService<IContextWriter>();

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ContextMiddleware<TestContext>(next);
        var httpContext = new DefaultHttpContext { RequestServices = provider };

        await middleware.InvokeAsync(httpContext, propagator, writer);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Middleware_ExtractsPartialContext_WhenSomeHeadersPresent()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")
            .MapProperty(c => c.UserId, "X-User-Id")));
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

        var middleware = new ContextMiddleware<TestContext>(next);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-Id"] = "t1";
        httpContext.RequestServices = provider;

        await middleware.InvokeAsync(httpContext, propagator, writer);

        Assert.NotNull(capturedContext);
        Assert.Equal("t1", capturedContext.TenantId);
        Assert.Null(capturedContext.UserId);
    }

    public class TestContext
    {
        public string? TenantId { get; set; }
        public string? UserId { get; set; }
    }
}
