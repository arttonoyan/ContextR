# Sample: JwtAdjunctContext

## Goal

Show how to use ContextR with JWT instead of trying to replace JWT.

JWT and ContextR solve different problems:

- JWT: identity, claims, authorization, trust boundaries
- ContextR: operational context propagation across service hops

## Recommended split

Keep in JWT:

- subject/user identity
- roles/permissions
- issuer/audience/security claims

Propagate with ContextR:

- correlation IDs
- tenant routing hints (if non-sensitive and already validated)
- request/workflow metadata
- non-security operational tags

## Registration example

```csharp
public sealed class OperationContext
{
    public string? TraceId { get; set; }
    public string? WorkflowId { get; set; }
    public string? TenantId { get; set; }
}

builder.Services.AddContextR(ctx =>
{
    ctx.Add<OperationContext>(reg => reg
        .MapProperty(c => c.TraceId, "X-Trace-Id")
        .MapProperty(c => c.WorkflowId, "X-Workflow-Id")
        .MapProperty(c => c.TenantId, "X-Tenant-Id")
        .UseAspNetCore()
        .UseGlobalHttpPropagation());
});
```

## Caveats and best practices

- do not move auth decisions from JWT to ContextR headers
- treat propagated operational values as untrusted until validated
- avoid sensitive PII in propagated context payloads
- apply strict size and failure policies for unbounded values

## Suggested tests

- JWT auth still controls authorization path
- ContextR metadata is present in downstream requests
- tampered propagated metadata is detected/validated by business rules
