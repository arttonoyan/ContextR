# ContextR.Propagation.Chunking

Chunking strategy package for oversized mapped payloads.

## Install

```bash
dotnet add package ContextR.Propagation.Chunking
```

## Why this package exists

`ContextR.Propagation.Mapping` defines mapping and orchestration.  
`ContextR.Propagation.Chunking` provides a concrete strategy that splits oversized serialized payloads into multiple keys and reassembles them on extraction.

This keeps mapping strategy-neutral and allows alternative chunking implementations in the future.

## Usage

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<UserContext>(reg => reg
        .UseInlineJsonPayloads<UserContext>(o =>
        {
            o.MaxPayloadBytes = 256;
            o.OversizeBehavior = ContextOversizeBehavior.ChunkProperty;
        })
        .UseChunkingPayloads<UserContext>()
        .MapProperty(c => c.Tags, "X-Tags"));
});
```

## Key format (default strategy)

- `<Key>__chunks` → number of chunks
- `<Key>__chunk_0`, `<Key>__chunk_1`, ... → chunk bodies

## Hybrid policy support

You can set a context-level default and override per property:

```csharp
.Map(m => m
    .DefaultOversizeBehavior(ContextOversizeBehavior.SkipProperty)
    .Property(c => c.Tags, "X-Tags").OversizeBehavior(ContextOversizeBehavior.ChunkProperty).Optional()
    .Property(c => c.Payload, "X-Payload").Optional())
```

Resolution order:
1. Property override
2. Context default
3. Fallback (`FailFast`)

## Testing

- `tests/ContextR.Propagation.Chunking.UnitTests`
- `tests/ContextR.Propagation.Strategies.IntegrationTests`
