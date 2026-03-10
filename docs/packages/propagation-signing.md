# Propagation Signing

`ContextR.Propagation.Signing` provides HMAC-based tamper detection for context headers across service boundaries.

[Back to Packages Overview](index.md){ .md-button }

## Use This Package When

- context flows across multiple services and must not be modified in transit
- you need tamper detection independent of transport security (defense-in-depth)
- context crosses untrusted or semi-trusted network boundaries

## Install

```bash
dotnet add package ContextR.Propagation.Signing
```

## Quick Start

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<TenantContext>(reg => reg
        .MapProperty(c => c.TenantId, "X-Tenant-Id")
        .MapProperty(c => c.Region, "X-Region")
        .UseContextSigning<TenantContext>(o =>
            o.Key = Convert.FromBase64String("your-base64-key-here"))
        .UseAspNetCore()
        .UseGlobalHttpPropagation());
});
```

No custom key provider needed — keys are configured inline.

## Depends On

- `ContextR.Propagation`

## See Also

- [Signing Details](../ContextR.Propagation.Signing.md)
- [Propagation Security Best Practices](../PropagationSecurity.md)
- [Packages Overview](index.md)
