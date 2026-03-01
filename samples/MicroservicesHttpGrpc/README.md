# Sample: MicroservicesHttpGrpc

## Scenario

Gateway receives HTTP traffic and calls:

- Service A over HTTP
- Service B over gRPC

Both paths must carry the same context model without duplicating mapping logic.

## Why this matters

- mixed transport stacks are common in modern .NET systems
- teams often implement inconsistent header/metadata adapters
- debugging is easier when correlation model is shared

## Context contract

```csharp
public sealed class CorrelationContext
{
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public List<string>? Tags { get; set; }
}
```

## Registration

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<CorrelationContext>(reg => reg
        .UseInlineJsonPayloads<CorrelationContext>(o =>
        {
            o.MaxPayloadBytes = 256;
            o.OversizeBehavior = ContextOversizeBehavior.SkipProperty;
        })
        .UseChunkingPayloads<CorrelationContext>()
        .UseStrategyPolicy<CorrelationContext>(sp => policyContext =>
            policyContext.Key == "X-Tags"
                ? ContextOversizeBehavior.ChunkProperty
                : ContextOversizeBehavior.SkipProperty)
        .MapProperty(c => c.TraceId, "X-Trace-Id")
        .MapProperty(c => c.SpanId, "X-Span-Id")
        .MapProperty(c => c.Tags, "X-Tags")
        .UseAspNetCore()
        .UseGlobalHttpPropagation()
        .UseGlobalGrpcPropagation());
});
```

## Flow

1. HTTP middleware extracts context at gateway boundary.
2. Outgoing HTTP handler injects mapped values for Service A.
3. gRPC interceptors inject/extract same context for Service B.
4. Oversize `Tags` follow runtime strategy policy.

## Suggested integration tests

- HTTP request round-trips trace/span across gateway and Service A
- gRPC call round-trips same trace/span to Service B
- oversize `Tags` are chunked and reassembled
- malformed payload follows configured failure policy
