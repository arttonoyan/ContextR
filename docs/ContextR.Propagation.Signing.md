# ContextR.Propagation.Signing

HMAC-based context signing for tamper detection across service boundaries.

## When to use this package

Use `ContextR.Propagation.Signing` when propagated context must not be modified in transit. The package signs all mapped property headers on inject and verifies them on extract using HMAC-SHA256.

This package does **not** provide encryption (confidentiality). For encryption scenarios, see [Encryption with DataProtection](EncryptionWithDataProtection.md).

## Install

```
dotnet add package ContextR.Propagation.Signing
```

Dependencies: `ContextR.Propagation`. No additional NuGet packages required — `HMACSHA256` is part of the .NET runtime.

## Quick start

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

That's it. All outgoing requests include an `X-Context-Signature` header. All incoming requests are verified before context is extracted.

No custom `ISigningKeyProvider` implementation is needed — the key is configured inline.

## How it works

### Inject (outgoing)

1. The inner propagator injects all mapped property headers normally
2. All injected key/value pairs are collected
3. Keys are sorted using `StringComparison.Ordinal` (byte-order, case-sensitive)
4. Signing input is built as `key1=value1\nkey2=value2\n` (trailing newline)
5. HMAC-SHA256 is computed over the UTF-8 bytes
6. The signature is encoded as `<base64url-hmac>.<keyVersion>` and added as a header

### Extract (incoming)

1. The signature header is read and decoded
2. All other context headers are collected during inner propagator extraction
3. The same canonical signing input is built
4. HMAC-SHA256 is recomputed using the key version from the signature header
5. Constant-time comparison verifies the signature
6. If valid, context is returned. If invalid, the failure handler is triggered

### Tamper detection coverage

The signing detects:

- **Modified values** — changing any header value invalidates the signature
- **Removed headers** — removing a header changes the signing input
- **Added headers** — adding a header that was not part of the original signed set changes the signing input

## Configuration

```csharp
.UseContextSigning<TenantContext>(o =>
{
    o.Key = hmacKeyBytes;                                  // inline key (simplest)
    o.SignatureHeader = "X-TenantContext-Sig";              // default: X-Context-Signature
})
```

## Key rotation

### Inline key rotation

For simple deployments, configure multiple key versions directly:

```csharp
.UseContextSigning<TenantContext>(o =>
{
    o.AddKey(1, Convert.FromBase64String("old-key-base64"));
    o.AddKey(2, Convert.FromBase64String("new-key-base64"));
    o.CurrentKeyVersion = 2;
})
```

The signature header includes the key version, enabling zero-downtime key rotation:

1. Deploy v2 key alongside v1 via `AddKey()`
2. Set `CurrentKeyVersion = 2` — new signatures use v2
3. After all in-flight requests drain, remove v1

### Custom key provider (advanced)

For dynamic key management (e.g., keys from Azure Key Vault, AWS Secrets Manager), implement `ISigningKeyProvider` and register it in DI:

```csharp
public sealed class VaultSigningKeyProvider : ISigningKeyProvider
{
    public byte[] GetKey(string keyId, int version) => /* fetch from vault */;
    public int GetCurrentVersion(string keyId) => /* current version from vault */;
}
```

```csharp
builder.Services.AddSingleton<ISigningKeyProvider, VaultSigningKeyProvider>();

builder.Services.AddContextR(ctx =>
{
    ctx.Add<TenantContext>(reg => reg
        .MapProperty(c => c.TenantId, "X-Tenant-Id")
        .UseContextSigning<TenantContext>(o => o.KeyId = "context-hmac-key")
        .UseAspNetCore()
        .UseGlobalHttpPropagation());
});
```

When `KeyId` is set (without inline keys), the registered `ISigningKeyProvider` is resolved from DI.

## Failure handling

Signing failures integrate with the existing propagation failure handler:

```csharp
ctx.Add<TenantContext>(reg => reg
    .MapProperty(c => c.TenantId, "X-Tenant-Id")
    .UseContextSigning<TenantContext>(o =>
        o.Key = hmacKeyBytes)
    .OnPropagationFailure<TenantContext>(failure =>
    {
        logger.LogWarning("Signing failure: {Reason} for {Key}",
            failure.RawValue, failure.Key);
        return PropagationFailureAction.SkipContext;
    }));
```

Failure reasons (available as constants on `SigningFailureReasons`):

| Reason | When |
|---|---|
| `SignatureInvalid` | HMAC verification failed (tampered headers) |
| `SignatureMissing` | No signature header on incoming request |
| `SignatureMalformed` | Signature header could not be parsed |
| `KeyNotFound` | Key provider could not resolve the key ID or version |

## Canonical signing input format

The signing input is deterministic and documented for cross-platform compatibility:

1. Collect all context header key/value pairs (excluding the signature header)
2. Sort by key using `StringComparison.Ordinal`
3. Format as `key=value\n` (newline-separated, trailing newline)
4. UTF-8 encode

Example for headers `X-Region: us-east-1` and `X-Tenant-Id: acme`:

```
X-Region=us-east-1
X-Tenant-Id=acme
```

## Signature header format

```
<base64url-hmac>.<keyVersion>
```

Example: `dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk.3`

The base64url encoding uses `-` instead of `+`, `_` instead of `/`, and no padding — safe for HTTP headers.

## File map

| File | Role |
|---|---|
| `ISigningKeyProvider.cs` | Key provider contract (advanced) |
| `SigningOptions.cs` | Configuration options with inline key support |
| `SigningFailureReasons.cs` | Failure reason constants |
| `ContextRSigningExtensions.cs` | `UseContextSigning<T>()` registration |
| `Internal/SigningContextPropagator.cs` | `IContextPropagator<T>` decorator |
| `Internal/InMemorySigningKeyProvider.cs` | Built-in key provider for inline keys |
| `Internal/CanonicalSigningInput.cs` | Deterministic signing input builder |
| `Internal/SignatureCodec.cs` | Base64url signature encode/decode |

## Testing

- `tests/ContextR.Propagation.Signing.UnitTests` — propagator round-trip, tamper detection, key rotation, canonical ordering, codec
- `tests/ContextR.Propagation.Signing.IntegrationTests` — end-to-end with ASP.NET Core TestHost and HttpClient propagation
