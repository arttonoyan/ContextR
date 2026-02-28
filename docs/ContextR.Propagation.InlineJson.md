# ContextR.Propagation.InlineJson

Inline JSON payload strategy for `MapProperty` in ContextR.

This package adds JSON serialization support for non-primitive mapped properties (`List<T>`, arrays, custom classes) and enforces deterministic payload-size policy.

## Install

```
dotnet add package ContextR.Propagation.InlineJson
```

Dependencies: `ContextR`, `ContextR.Propagation`.

## Quick start

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<RequestContext>(reg => reg
        .UseInlineJsonPayloads<RequestContext>(o =>
        {
            o.MaxPayloadBytes = 4096;
            o.OversizeBehavior = ContextOversizeBehavior.FailFast;
        })
        .MapProperty(c => c.Tags, "X-Tags")
        .MapProperty(c => c.Payload, "X-Payload"));
});
```

## Oversize behavior

- `FailFast` -- throws deterministic `InvalidOperationException` when payload exceeds size cap.
- `SkipProperty` -- skips only the oversize property; other mapped properties continue.
- `FallbackToToken` -- signals token fallback intent; currently throws deterministic error if token strategy runtime is not configured.

## What is considered "complex"

`InlineJsonPayloadSerializer<TContext>` handles non-simple types and keeps simple transport types on existing mapping behavior:

- Treated as simple (not JSON): `string`, enums, `IParsable<T>` types, convertible primitives.
- Treated as complex (JSON): `List<T>`, arrays, and regular custom classes.

## API

- `UseInlineJsonPayloads<TContext>()`
- `UseInlineJsonPayloads<TContext>(Action<InlineJsonPayloadOptions> configure)`
- `InlineJsonPayloadOptions.MaxPayloadBytes`
- `InlineJsonPayloadOptions.OversizeBehavior`

## Testing

Coverage is provided by:

- `tests/ContextR.Propagation.InlineJson.UnitTests`
- `tests/ContextR.Propagation.Strategies.IntegrationTests` (real `Microsoft.AspNetCore.TestHost` end-to-end scenarios)
