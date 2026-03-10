# Propagation Security Best Practices

Guidelines for securing context propagation across service boundaries.

## TLS first

Transport-level security (TLS/mTLS) is the baseline for all service-to-service communication. Context signing and encryption are **defense-in-depth** measures, not replacements for transport security.

## When to sign

Use `UseContextSigning()` when:

- Context flows through **2+ service boundaries** where any hop could modify headers
- You need **tamper detection** independent of transport security
- Context passes through **third-party gateways or proxies** you don't fully control
- You want to detect accidental header corruption or middleware bugs

For most production microservice deployments, signing is the recommended default.

## When to encrypt

Use encryption (see [Encryption with DataProtection](EncryptionWithDataProtection.md)) when:

- Context crosses **public internet** or untrusted network segments
- Headers contain operational data that should not be visible to intermediaries
- Regulatory or compliance requirements mandate payload-level encryption

For internal service mesh traffic over mTLS, encryption adds overhead without significant benefit.

## Don't put secrets in context

Even with encryption, avoid propagating:

- Passwords, API keys, or authentication tokens
- Personally identifiable information (PII) subject to data protection regulations
- Data that requires audit trails for access

For sensitive data, use the [Token strategy](ContextR.Propagation.Token.md) with a secure backing store (Redis, database) so only a reference token travels in headers.

## Key rotation

### Signing keys

The signing package supports zero-downtime key rotation via inline keys or a custom `ISigningKeyProvider`:

```csharp
.UseContextSigning<TenantContext>(o =>
{
    o.AddKey(1, oldKeyBytes);
    o.AddKey(2, newKeyBytes);
    o.CurrentKeyVersion = 2;
})
```

1. **Deploy new key version** — add both old (v1) and new (v2) via `AddKey()`
2. **Switch current version** — set `CurrentKeyVersion = 2`. New signatures use v2; verification still accepts v1 from the signature header
3. **Drain and revoke** — once all in-flight requests using v1 have completed, remove v1

For dynamic key management (vault-backed keys), implement `ISigningKeyProvider` and register it in DI with `o.KeyId = "..."`. See [Signing Details](ContextR.Propagation.Signing.md#custom-key-provider-advanced).

### Encryption keys (Data Protection)

ASP.NET Core Data Protection handles key rotation automatically. Keys have a default 90-day lifetime and are rotated transparently. See [Microsoft docs](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-management) for configuration.

## Cache keys eagerly (custom key providers)

When using a custom `ISigningKeyProvider` (instead of inline keys), note that the interface is synchronous because the propagation pipeline (`IContextPropagator<T>`) is synchronous. Implementations should:

- Load keys from the vault at application startup
- Cache keys in memory
- Refresh periodically in the background (not on the request path)

Blocking on async key retrieval during request processing adds latency and risks timeouts.

For most deployments, inline keys via `o.Key` or `o.AddKey()` avoid this concern entirely.

## Header size budget

- **Signing** adds one header (~60 bytes for base64url HMAC-SHA256 + version)
- **Encryption** increases total header size by ~2x due to the DataProtection envelope + base64 encoding

Account for these increases when configuring `MaxPayloadBytes` and HTTP server header limits.
