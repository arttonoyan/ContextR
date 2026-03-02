# Sample: GatewayIngressResolution

## Scenario

A gateway receives external requests, authenticates JWT, builds `UserContext`, and forwards calls to downstream services.

Requirements:

- at external ingress, context must come from trusted resolver source (JWT claims)
- inside internal mesh, propagated context should continue flowing between services
- business services should read stable snapshot values

## Context model

```csharp
public sealed class UserContext
{
    public string? TenantId { get; set; }
    public string? UserId { get; set; }
    public string? TraceId { get; set; }
    public string[] Roles { get; set; } = [];
}
```

## Step 1: register core + resolution + propagation

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<UserContext>(reg => reg
        .UseResolver<UserContext, JwtClaimsUserContextResolver>()
        .MapProperty(c => c.TenantId, "X-Tenant-Id")
        .MapProperty(c => c.UserId, "X-User-Id")
        .MapProperty(c => c.TraceId, "X-Trace-Id")
        .UseAspNetCore()
        .UseGlobalHttpPropagation());
});
```

`UseResolver(...)` auto-registers resolution services, so no separate `AddContextRResolution()` call is needed in this flow.

## Step 2: implement resolver (JWT -> UserContext)

```csharp
using System.Security.Claims;
using ContextR.Resolution;
using Microsoft.AspNetCore.Http;

public sealed class JwtClaimsUserContextResolver : IContextResolver<UserContext>
{
    private readonly IHttpContextAccessor _http;

    public JwtClaimsUserContextResolver(IHttpContextAccessor http)
    {
        _http = http;
    }

    public UserContext? Resolve(ContextResolutionContext context)
    {
        var principal = _http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return null;

        return new UserContext
        {
            TenantId = principal.FindFirstValue("tenant_id"),
            UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier),
            TraceId = _http.HttpContext?.TraceIdentifier,
            Roles = principal.FindAll(ClaimTypes.Role).Select(r => r.Value).ToArray()
        };
    }
}
```

## Step 3: resolve at ingress (external boundary)

```csharp
public sealed class GatewayContextInitializationMiddleware
{
    private readonly RequestDelegate _next;

    public GatewayContextInitializationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        IContextResolutionOrchestrator<UserContext> orchestrator,
        IContextPropagator<UserContext> propagator)
    {
        // Optional propagated value if present; for external callers this is usually ignored by default policy.
        var propagated = propagator.Extract(
            httpContext.Request.Headers,
            static (headers, key) => headers.TryGetValue(key, out var values) ? (string?)values : null);

        orchestrator.ResolveAndWrite(
            new ContextResolutionContext
            {
                Boundary = ContextIngressBoundary.External,
                Source = "gateway-http-jwt"
            },
            propagated);

        await _next(httpContext);
    }
}
```

## Step 4: consume snapshot in business services

```csharp
public sealed class OrdersService
{
    private readonly IContextSnapshot _snapshot;

    public OrdersService(IContextSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public string? CurrentTenant() => _snapshot.GetContext<UserContext>()?.TenantId;
}
```

## End-to-end behavior

1. Gateway authenticates request.
2. Resolver builds `UserContext` from claims.
3. Orchestrator applies trust-boundary policy (external -> resolver wins).
4. Context is written to ambient store.
5. Outgoing `HttpClient` propagation injects mapped headers.
6. Downstream internal services continue propagation and can apply internal boundary rules.

## Suggested tests

- external request with forged propagated headers still resolves from JWT claims
- internal request between services prefers propagated context by default
- missing JWT claims returns null context and does not crash
- parallel gateway requests keep tenant/user isolation
