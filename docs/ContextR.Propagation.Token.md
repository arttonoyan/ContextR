# ContextR.Propagation.Token

Token/reference contracts for large payload transport strategies.

This package defines the contracts needed to store large serialized payloads out-of-band and propagate only a compact token in headers/metadata.

## Install

```
dotnet add package ContextR.Propagation.Token
```

Dependencies: `ContextR.Propagation`.

## Contracts

- `ContextPayloadTokenReference` -- token envelope (`Token`, optional `Version`).
- `IContextPayloadStore` -- storage abstraction:
  - `PutAsync(payload, ttl)`
  - `GetAsync(token)`
  - `DeleteAsync(token)`
- `IContextPayloadTokenCodec` -- encode/decode token envelope to transport-safe string.

## Intended usage

Typical flow for large payload scenarios:

1. Serialize mapped payload.
2. If payload is over inline threshold, write it to store via `IContextPayloadStore`.
3. Encode token reference via `IContextPayloadTokenCodec`.
4. Propagate token string in HTTP/gRPC metadata instead of full payload.
5. On extraction, decode token and hydrate payload from store.

## Current status

- Contracts are available in this package.
- Store-backed runtime fallback wiring is planned as a next step.

## Testing

Coverage is provided by:

- `tests/ContextR.Propagation.Token.UnitTests`
- `tests/ContextR.Propagation.Strategies.IntegrationTests` (fallback diagnostics when no token runtime is configured)
