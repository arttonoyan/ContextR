# Sample: HttpContextAccessorReplacement

## Goal

Replace direct `IHttpContextAccessor` usage in application services with explicit ContextR context/snapshot patterns.

## Typical anti-pattern

```csharp
public sealed class PricingService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PricingService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetTenant()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Id"];
    }
}
```

Problems:

- tightly coupled to HTTP runtime
- hard to test outside ASP.NET integration tests
- brittle in background tasks and asynchronous fan-out

## ContextR replacement

```csharp
public sealed class UserContext
{
    public string? TenantId { get; set; }
    public string? TraceId { get; set; }
    public string? UserId { get; set; }
}
```

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<UserContext>(reg => reg
        .MapProperty(c => c.TenantId, "X-Tenant-Id")
        .MapProperty(c => c.TraceId, "X-Trace-Id")
        .MapProperty(c => c.UserId, "X-User-Id")
        .UseAspNetCore()
        .UseGlobalHttpPropagation());
});
```

```csharp
public sealed class PricingService
{
    private readonly IContextSnapshot _snapshot;

    public PricingService(IContextSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public string? GetTenant()
    {
        return _snapshot.GetContext<UserContext>()?.TenantId;
    }
}
```

## Why snapshot model is useful

- explicit contract instead of magic header reads
- deterministic data view during operation lifetime
- works in non-web and background execution
- easier unit testing without full HTTP stack

## Suggested tests

- `PricingService` unit test with synthetic `IContextSnapshot`
- integration test proving inbound header extraction still works
- background dispatch test using `snapshot.BeginScope()`
