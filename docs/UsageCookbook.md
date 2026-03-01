# Usage Cookbook

Practical patterns for real services using ContextR.

## Pattern: required vs optional mapping

Use `Required()` for fields that must be present and valid:

```csharp
ctx.Add<UserContext>(reg => reg
    .Map(m => m
        .Property(c => c.TraceId, "X-Trace-Id").Required()
        .Property(c => c.TenantId, "X-Tenant-Id").Required()
        .Property(c => c.UserId, "X-User-Id").Optional()));
```

## Pattern: non-primitive property mapping

For `List<T>`, arrays, or custom classes:

```csharp
ctx.Add<UserContext>(reg => reg
    .UseInlineJsonPayloads<UserContext>(o =>
    {
        o.MaxPayloadBytes = 4096;
        o.OversizeBehavior = ContextOversizeBehavior.FailFast;
    })
    .MapProperty(c => c.Roles, "X-Roles")
    .MapProperty(c => c.Profile, "X-Profile"));
```

## Pattern: oversize strategy matrix

| Behavior | Inject | Extract | Typical use |
|---|---|---|---|
| `FailFast` | throws | throws | strict contracts |
| `SkipProperty` | drops property | property missing | non-critical metadata |
| `ChunkProperty` | writes chunk keys | reassembles chunks | bigger metadata in headers/metadata |
| `FallbackToToken` | token fallback path | token lookup path | very large payloads with external store |

## Pattern: hybrid policy precedence

Oversize behavior selection order:

1. property override (`OversizeBehavior(...)` / `MapProperty(..., override)`)
2. mapping default (`DefaultOversizeBehavior(...)`)
3. runtime strategy policy (`UseStrategyPolicy(...)`)
4. transport default (`UseInlineJsonPayloads(...).OversizeBehavior`)
5. `FailFast`

## Pattern: strategy policy service

```csharp
ctx.Add<UserContext>(reg => reg
    .UseInlineJsonPayloads<UserContext>(o => o.MaxPayloadBytes = 256)
    .UseChunkingPayloads<UserContext>()
    .UseStrategyPolicy<UserContext, UserStrategyPolicy>()
    .MapProperty(c => c.Roles, "X-Roles")
    .MapProperty(c => c.Profile, "X-Profile"));
```

## Pattern: strategy policy delegate from DI

```csharp
ctx.Add<UserContext>(reg => reg
    .UseInlineJsonPayloads<UserContext>(o => o.MaxPayloadBytes = 256)
    .UseChunkingPayloads<UserContext>()
    .UseStrategyPolicy<UserContext>(sp => policyContext =>
    {
        return policyContext.Key == "X-Roles"
            ? ContextOversizeBehavior.ChunkProperty
            : ContextOversizeBehavior.SkipProperty;
    })
    .MapProperty(c => c.Roles, "X-Roles")
    .MapProperty(c => c.Profile, "X-Profile"));
```

## Pattern: domain-aware failure handling

```csharp
ctx.Add<UserContext>(reg => reg
    .OnPropagationFailure<UserContext>(_ => PropagationFailureAction.Throw));

ctx.AddDomain("public-api", d => d.Add<UserContext>(reg => reg
    .OnPropagationFailure<UserContext>(_ => PropagationFailureAction.SkipProperty)));
```

## Pattern: background processing with snapshots

```csharp
var snapshot = accessor.CreateSnapshot();

_ = Task.Run(async () =>
{
    using (snapshot.BeginScope())
    {
        await processor.RunAsync();
    }
});
```

This avoids accidental coupling to request pipeline state and keeps propagation deterministic.
