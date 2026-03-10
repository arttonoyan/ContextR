# Encryption with ASP.NET Core Data Protection

For scenarios where context must be encrypted (not just signed), ASP.NET Core's Data Protection API provides a complete solution with built-in key management, rotation, and authenticated encryption.

## When to use encryption

- Context crosses **public internet** (partner APIs, cross-cloud, edge-to-origin)
- Regulatory or compliance requirements demand **defense-in-depth** beyond TLS
- Context contains operational data you don't want intermediaries to see

For most internal service-to-service communication over mTLS, encryption is unnecessary — use [context signing](ContextR.Propagation.Signing.md) for tamper detection instead.

## Prerequisites

All services that exchange encrypted context must share a Data Protection key ring:

```csharp
builder.Services.AddDataProtection()
    .SetApplicationName("my-service-mesh")
    .PersistKeysToAzureBlobStorage(connectionString, "container", "keys.xml");
    // or .PersistKeysToStackExchangeRedis(...)
    // or .PersistKeysToFileSystem(...)
```

See [Microsoft docs](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview) for key storage options.

## Implementation

Create a custom `IContextPropagator<T>` that wraps the inner propagator and encrypts/decrypts the entire context as a single header:

```csharp
using System.Text.Json;
using ContextR.Propagation;
using Microsoft.AspNetCore.DataProtection;

public sealed class DataProtectionPropagator<TContext> : IContextPropagator<TContext>
    where TContext : class
{
    private readonly IContextPropagator<TContext> _inner;
    private readonly IDataProtector _protector;
    private const string EnvelopeHeader = "X-Context-Protected";

    public DataProtectionPropagator(
        IContextPropagator<TContext> inner,
        IDataProtectionProvider provider)
    {
        _inner = inner;
        _protector = provider.CreateProtector("ContextR.Propagation");
    }

    public void Inject<TCarrier>(TContext context, TCarrier carrier,
        Action<TCarrier, string, string> setter)
    {
        var pairs = new Dictionary<string, string>();
        _inner.Inject(context, pairs,
            static (dict, key, value) => dict[key] = value);

        if (pairs.Count == 0)
            return;

        var json = JsonSerializer.Serialize(pairs);
        var encrypted = _protector.Protect(json);
        setter(carrier, EnvelopeHeader, encrypted);
    }

    public TContext? Extract<TCarrier>(TCarrier carrier,
        Func<TCarrier, string, string?> getter)
    {
        var encrypted = getter(carrier, EnvelopeHeader);
        if (encrypted is null)
            return null;

        string json;
        try
        {
            json = _protector.Unprotect(encrypted);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }

        var pairs = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (pairs is null)
            return null;

        return _inner.Extract(pairs,
            static (dict, key) => dict.TryGetValue(key, out var v) ? v : null);
    }
}
```

## Registration

Register the custom propagator **before** `MapProperty` calls so it wraps the mapping propagator:

```csharp
builder.Services.AddDataProtection()
    .SetApplicationName("my-service-mesh")
    .PersistKeysToAzureBlobStorage(connectionString, "container", "keys.xml");

builder.Services.AddContextR(ctx =>
{
    ctx.Add<TenantContext>(reg => reg
        .UsePropagator<TenantContext, DataProtectionPropagator<TenantContext>>()
        .MapProperty(c => c.TenantId, "X-Tenant-Id")
        .MapProperty(c => c.Region, "X-Region")
        .UseAspNetCore()
        .UseGlobalHttpPropagation());
});
```

## Trade-offs

| Aspect | Signing | Encryption (DataProtection) |
|---|---|---|
| Header visibility | Keys and values visible, signature header added | Single opaque header |
| Header size | Original headers + ~60 bytes signature | ~2x due to encryption envelope |
| Key management | Inline keys or custom `ISigningKeyProvider` | Automatic via Data Protection key ring |
| Cross-platform | Canonical format documented for interop | .NET Data Protection format only |
| Debugging | Headers readable in logs and traces | Opaque blob — harder to debug |
| Protection | Tamper detection only | Confidentiality + tamper detection |

## Combining signing and encryption

If you use the `DataProtectionPropagator` shown above, the encrypted output already includes authenticated encryption (tamper detection is built into Data Protection). Adding `UseContextSigning` on top would be redundant.

Use signing alone when you need readable headers with tamper detection. Use encryption when you need confidentiality.
