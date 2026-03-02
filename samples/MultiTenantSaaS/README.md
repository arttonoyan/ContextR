# Sample: MultiTenantSaaS

## Scenario

You run a multi-tenant SaaS API.  
Every call must preserve tenant identity, correlation IDs, and user metadata across downstream services.

## Why this matters

- tenant leakage is a security risk
- correlation loss hurts incident debugging
- repeated manual header code causes drift between services

## Context contract

```csharp
public sealed class UserContext
{
    public required string TenantId { get; set; }
    public required string TraceId { get; set; }
    public string? UserId { get; set; }
}
```

## Registration

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<UserContext>(reg => reg
        .Map(m => m
            .ByConvention()
            .Property(c => c.TenantId, "X-Tenant-Id")
            .Property(c => c.TraceId, "X-Trace-Id")
            .Property(c => c.UserId, "X-User-Id").Optional())
        .UseAspNetCore()
        .UseGlobalHttpPropagation());
});
```

## Flow

1. API middleware extracts incoming headers into `UserContext`.  
2. Business services consume `IContextSnapshot` (not raw `HttpContext`).  
3. Outgoing `HttpClient` propagation writes mapped headers to downstream calls.  
4. Missing required fields fail fast or follow custom failure policy.

## Suggested integration tests

- request with all headers propagates unchanged to downstream service
- missing `X-Tenant-Id` is rejected (required)
- missing optional `X-User-Id` still succeeds
- parallel requests with different tenants remain isolated
