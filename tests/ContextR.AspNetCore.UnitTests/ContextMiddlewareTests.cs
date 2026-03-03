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

    [Fact]
    public async Task Middleware_EnforcementFailRequest_ReturnsBadRequest_WhenRequiredContextMissing()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .Map(m => m.Property(c => c.TenantId, "X-Tenant-Id").Required())
            .UseAspNetCore(o => o.Enforcement(e => e.Mode = ContextIngressEnforcementMode.FailRequest))));
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

        await middleware.InvokeAsync(httpContext, propagator, writer, provider);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_EnforcementObserveOnly_AllowsRequest_WhenRequiredContextMissing()
    {
        var services = new ServiceCollection();
        var callbackInvoked = false;
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .Map(m => m.Property(c => c.TenantId, "X-Tenant-Id").Required())
            .UseAspNetCore(o => o.Enforcement(e =>
            {
                e.Mode = ContextIngressEnforcementMode.ObserveOnly;
                e.OnFailure = _ =>
                {
                    callbackInvoked = true;
                    return ContextIngressFailureDecision.Continue();
                };
            }))));
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

        await middleware.InvokeAsync(httpContext, propagator, writer, provider);

        Assert.True(nextCalled);
        Assert.True(callbackInvoked);
    }

    [Fact]
    public async Task Middleware_EnforcementCustomFailureDecision_WritesCustomStatus()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .Map(m => m.Property(c => c.TenantId, "X-Tenant-Id").Required())
            .UseAspNetCore(o => o.Enforcement(e =>
            {
                e.Mode = ContextIngressEnforcementMode.FailRequest;
                e.OnFailure = _ => ContextIngressFailureDecision.Fail(StatusCodes.Status422UnprocessableEntity, "tenant required");
            }))));
        using var provider = services.BuildServiceProvider();

        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();
        var writer = provider.GetRequiredService<IContextWriter>();

        var middleware = new ContextMiddleware<TestContext>(_ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext { RequestServices = provider };

        await middleware.InvokeAsync(httpContext, propagator, writer, provider);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_EnforcementFallback_CanProvideContext_AndContinue()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .Map(m => m.Property(c => c.TenantId, "X-Tenant-Id").Required())
            .UseAspNetCore(o => o.Enforcement(e =>
            {
                e.Mode = ContextIngressEnforcementMode.FailRequest;
                e.FallbackContextFactory = _ => new TestContext { TenantId = "fallback-tenant" };
            }))));
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
        var httpContext = new DefaultHttpContext { RequestServices = provider };

        await middleware.InvokeAsync(httpContext, propagator, writer, provider);

        Assert.NotNull(capturedContext);
        Assert.Equal("fallback-tenant", capturedContext.TenantId);
    }

    public class TestContext
    {
        public string? TenantId { get; set; }
        public string? UserId { get; set; }
    }
}
