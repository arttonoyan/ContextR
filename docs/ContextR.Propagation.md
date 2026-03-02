# ContextR.Propagation.Mapping

Property-based context propagation for ContextR. This package provides fluent mapping APIs that auto-generate `IContextPropagator<T>` implementations from property-to-key mappings -- no boilerplate serialization code required.

## When to use this package

Use `ContextR.Propagation.Mapping` when your context classes are simple POCOs with properties that map 1:1 to transport keys (HTTP headers, gRPC metadata, Kafka headers, etc.).

For complex serialization logic -- conditional fields, composite values, encrypted payloads -- implement `IContextPropagator<T>` directly and register it with `UsePropagator<TContext, TPropagator>()` instead.

## Install

```
dotnet add package ContextR.Propagation.Mapping
```

Dependencies: `ContextR` (core), `ContextR.Propagation`.

## Quick start

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<CorrelationContext>(reg => reg
        .MapProperty(c => c.TraceId, "X-Trace-Id")
        .MapProperty(c => c.SpanId, "X-Span-Id"));
});

public class CorrelationContext
{
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
}
```

Each `MapProperty` call tells the framework: "when injecting this context into a transport carrier, write property `TraceId` under key `X-Trace-Id`; when extracting, read key `X-Trace-Id` and set it back on `TraceId`."

### Advanced DSL with required/optional

```csharp
ctx.Add<CorrelationContext>(reg => reg
    .Map(m => m
        .DefaultOversizeBehavior(ContextOversizeBehavior.SkipProperty)
        .Property(c => c.TraceId, "X-Trace-Id").Required()
        .Property(c => c.SpanId, "X-Span-Id").OversizeBehavior(ContextOversizeBehavior.ChunkProperty).Optional()));
```

- `Required()` means missing/invalid values fail by default.
- `Optional()` means missing/invalid values are ignored.
- `DefaultOversizeBehavior(...)` sets context-level oversize strategy default for DSL mappings.
- `OversizeBehavior(...)` on a property overrides the context-level default for that property.

### Nullability conventions (default)

By default, mapping infers requirement level from C# nullability:

- non-nullable property => required
- nullable property => optional

```csharp
ctx.Add<CorrelationContext>(reg => reg
    .Map(m => m
        .ByConvention()
        .Property(c => c.TraceId, "X-Trace-Id")
        .Property(c => c.SpanId, "X-Span-Id")));
```

If you prefer explicit per-property convention:

```csharp
ctx.Add<CorrelationContext>(reg => reg
    .Map(m => m
        .Property(c => c.TraceId, "X-Trace-Id").ByConvention()
        .Property(c => c.SpanId, "X-Span-Id").ByConvention()));
```

Explicit calls still win:

```csharp
.Property(c => c.SpanId, "X-Span-Id").Optional()
```

Disable conventions for fully manual requirement control:

```csharp
ctx.Add<CorrelationContext>(reg => reg
    .DisableNullabilityConventions()
    .Map(m => m
        .Property(c => c.TraceId, "X-Trace-Id").Required()
        .Property(c => c.SpanId, "X-Span-Id").Optional()));
```

### Runtime oversize strategy policy

When you need runtime decisions (per key, direction, domain, payload size), register a strategy policy:

```csharp
ctx.Add<UserContext>(reg => reg
    .UseInlineJsonPayloads<UserContext>(o => o.MaxPayloadBytes = 256)
    .UseChunkingPayloads<UserContext>()
    .UseStrategyPolicy<UserContext, UserStrategyPolicy>()
    .MapProperty(c => c.Roles, "X-Roles")
    .MapProperty(c => c.Profile, "X-Profile"));

public sealed class UserStrategyPolicy : IContextPropagationStrategyPolicy<UserContext>
{
    public ContextOversizeBehavior Select(ContextPropagationStrategyPolicyContext context)
    {
        return context.Key == "X-Roles"
            ? ContextOversizeBehavior.ChunkProperty
            : ContextOversizeBehavior.SkipProperty;
    }
}
```

Delegate-based registration is also supported:

```csharp
ctx.Add<UserContext>(reg => reg
    .UseInlineJsonPayloads<UserContext>(o => o.MaxPayloadBytes = 256)
    .UseChunkingPayloads<UserContext>()
    .UseStrategyPolicy<UserContext>(sp => policyContext =>
        policyContext.Key == "X-Roles"
            ? ContextOversizeBehavior.ChunkProperty
            : ContextOversizeBehavior.SkipProperty)
    .MapProperty(c => c.Roles, "X-Roles")
    .MapProperty(c => c.Profile, "X-Profile"));
```

Oversize decision precedence:

1. property override (`OversizeBehavior(...)` / `MapProperty(..., oversizeBehaviorOverride)`)
2. mapping default (`DefaultOversizeBehavior(...)`)
3. runtime strategy policy (`UseStrategyPolicy(...)`)
4. transport policy default (`UseInlineJsonPayloads(...).OversizeBehavior`)
5. `FailFast`

## How it works

### Registration

Each `MapProperty` call registers an `IPropertyMapping<TContext>` singleton into DI. On the first call, a `MappingContextPropagator<TContext>` is also registered as the `IContextPropagator<TContext>` implementation. The propagator collects all property mappings at construction time.

```
MapProperty(c => c.TraceId, "X-Trace-Id")
  → registers IPropertyMapping<CorrelationContext> (TraceId ↔ "X-Trace-Id")
  → registers IContextPropagator<CorrelationContext> = MappingContextPropagator (TryAdd)

MapProperty(c => c.SpanId, "X-Span-Id")
  → registers IPropertyMapping<CorrelationContext> (SpanId ↔ "X-Span-Id")
  → IContextPropagator already registered, TryAdd is a no-op
```

### Injection (context → carrier)

When a transport layer needs to inject context into a carrier (e.g., HTTP headers), the propagator iterates all property mappings:

```
For each mapping:
    value = getter(context)          // compiled expression, reads the property
    if value is not null:
        setter(carrier, key, value)  // writes to the carrier (e.g., headers.Add)
```

Null properties are skipped -- only non-null values are injected.

### Extraction (carrier → context)

When a transport layer extracts context from a carrier, the propagator creates a new instance and populates it:

```
context = new TContext()             // Activator.CreateInstance
anySet = false

For each mapping:
    raw = getter(carrier, key)       // reads from the carrier (e.g., headers["X-Trace-Id"])
    if raw is not null:
        if TryParse(raw, out parsed):
            setter(context, parsed)  // compiled expression, sets the property
            anySet = true

return anySet ? context : null       // null when no keys were found
```

If no keys were found in the carrier, `Extract` returns `null` -- the context is not created.

## Supported property types (default behavior)

| Type | Parsing strategy |
|---|---|
| `string` | Direct assignment, no parsing |
| Types implementing `IParsable<T>` | `T.TryParse(value, null, out result)` via reflection. Covers `int`, `long`, `double`, `decimal`, `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `bool`, and all other .NET types with `IParsable<T>`. |
| Other types | Fallback to `Convert.ChangeType(value, type)`. Throws on failure. |

Without an explicit payload strategy, complex mapped types like `List<T>`, arrays, and custom classes do not reliably round-trip.

## Payload strategy extensions

`ContextR.Propagation` provides strategy hooks for mapped property payload behavior:

```csharp
ctx.Add<UserContext>(reg => reg
    .UsePayloadSerializer<UserContext, CustomSerializer>()
    .UseTransportPolicy<UserContext, CustomPolicy>()
    .MapProperty(c => c.Roles, "X-Roles"));
```

Available abstractions:

- `IContextPayloadSerializer<TContext>` -- serialize/deserialize mapped property payloads
- `IContextTransportPolicy<TContext>` -- payload size constraints + oversize behavior
- `IContextPayloadChunkingStrategy<TContext>` -- strategy contract for chunk split/reassembly
- `IContextPropagationStrategyPolicy<TContext>` -- runtime oversize strategy selection
- `ContextPropagationStrategyPolicyContext` -- policy input (key, type, direction, domain, payload bytes)
- `ContextOversizeBehavior` -- `FailFast`, `SkipProperty`, `ChunkProperty`, `FallbackToToken`

For production-ready non-primitive support, combine dedicated strategy packages:

- `ContextR.Propagation.InlineJson` for serializer + transport policy
- `ContextR.Propagation.Chunking` for chunk split/reassembly when using `ChunkProperty`

## Failure handling extensions

`ContextR.Propagation` also exposes failure handling hooks:

```csharp
ctx.Add<CorrelationContext>(reg => reg
    .OnPropagationFailure<CorrelationContext>(failure =>
    {
        // log/metrics/alerts here
        return PropagationFailureAction.SkipProperty;
    })
    .Map(m => m.Property(c => c.TraceId, "X-Trace-Id").Required()));
```

Available contracts:

- `IContextPropagationFailureHandler<TContext>`
- `PropagationFailureContext`
- `PropagationFailureReason`
- `PropagationFailureAction` (`Throw`, `SkipProperty`, `SkipContext`)
- `PropagationDirection` (`Inject`, `Extract`)

### Examples

```csharp
public class UserContext
{
    public string? TenantId { get; set; }       // string → direct
    public string? UserId { get; set; }         // string → direct
    public Guid SessionId { get; set; }         // Guid → IParsable
    public DateTime LastSeenUtc { get; set; }   // DateTime → IParsable
}

ctx.Add<UserContext>(reg => reg
    .MapProperty(c => c.TenantId, "X-Tenant-Id")
    .MapProperty(c => c.UserId, "X-User-Id")
    .MapProperty(c => c.SessionId, "X-Session-Id")
    .MapProperty(c => c.LastSeenUtc, "X-Last-Seen-Utc"));
```

## Requirements for context types

The `MappingContextPropagator` creates context instances via `Activator.CreateInstance<T>()` during extraction. This requires:

1. **Public parameterless constructor** -- the context class must have one, or `MappingContextPropagator` throws `InvalidOperationException` at construction time with a descriptive message.
2. **Writable properties** -- all mapped properties must have a public setter. `MapProperty` validates this at registration time.

```csharp
// Valid
public class CorrelationContext
{
    public string? TraceId { get; set; }
}

// Invalid -- no parameterless constructor
public class CorrelationContext
{
    public CorrelationContext(string traceId) => TraceId = traceId;
    public string TraceId { get; }
}
```

If your context type does not meet these requirements, use `UsePropagator<TContext, TPropagator>()` with a custom `IContextPropagator<T>` implementation that handles construction and population.

## MapProperty vs UsePropagator

`MapProperty` and `UsePropagator` are mutually exclusive for a given context type. The first one registered wins (both use `TryAdd` internally):

```csharp
// MapProperty wins -- registers IContextPropagator<T> as MappingContextPropagator
ctx.Add<CorrelationContext>(reg => reg
    .MapProperty(c => c.TraceId, "X-Trace-Id")
    .UsePropagator<CorrelationContext, CustomPropagator>());  // no-op, MappingContextPropagator already registered

// UsePropagator wins -- registers IContextPropagator<T> as CustomPropagator
ctx.Add<CorrelationContext>(reg => reg
    .UsePropagator<CorrelationContext, CustomPropagator>()
    .MapProperty(c => c.TraceId, "X-Trace-Id"));  // adds mapping, but propagator is CustomPropagator
```

In practice, pick one approach per context type and stick with it.

## MapProperty vs Map DSL

- Use `MapProperty(...)` for quick, straightforward mappings.
- Use `Map(...)` DSL when you need per-property requirement (`Required`/`Optional`) and richer policy-driven configuration.

## Custom propagator with other mapping libraries

`UsePropagator<TContext, TPropagator>()` is the extension point for integrating any serialization strategy. For example, with AutoMapper:

```csharp
public class AutoMapperPropagator<TContext> : IContextPropagator<TContext>
    where TContext : class
{
    private readonly IMapper _mapper;

    public AutoMapperPropagator(IMapper mapper) => _mapper = mapper;

    public void Inject<TCarrier>(TContext context, TCarrier carrier,
        Action<TCarrier, string, string> setter)
    {
        var dict = _mapper.Map<Dictionary<string, string>>(context);
        foreach (var (key, value) in dict)
            setter(carrier, key, value);
    }

    public TContext? Extract<TCarrier>(TCarrier carrier,
        Func<TCarrier, string, string?> getter)
    {
        // Build dictionary from carrier, then map
        var dict = new Dictionary<string, string>();
        foreach (var key in GetExpectedKeys())
        {
            var value = getter(carrier, key);
            if (value is not null) dict[key] = value;
        }
        return dict.Count > 0 ? _mapper.Map<TContext>(dict) : null;
    }
}
```

Register with:

```csharp
ctx.Add<CorrelationContext>(reg => reg
    .UsePropagator<CorrelationContext, AutoMapperPropagator<CorrelationContext>>());
```

## Guard clauses

`MapProperty` validates its arguments eagerly:

| Argument | Validation |
|---|---|
| `property` | `ArgumentNullException` if `null` |
| `property` expression | `ArgumentException` if not a property access expression (e.g., a method call) |
| `property` expression | `ArgumentException` if the member is a field instead of a property |
| `property` expression | `ArgumentException` if the property is read-only (no setter) |
| `key` | `ArgumentNullException` if `null`, `ArgumentException` if empty or whitespace |

## Internals

### IPropertyMapping&lt;TContext&gt;

Internal interface representing a single property-to-key mapping. Has four key members:

- `Key` -- the transport key name
- `GetValues(TContext)` -- reads property and returns one-or-many key/value pairs for injection
- `GetRawValue(...)` -- reads direct or strategy-derived raw payload from carrier
- `TrySetValue(TContext, string)` -- parses the string and sets the property, returns `false` on parse failure

### PropertyMapping&lt;TContext, TProperty&gt;

Internal implementation of `IPropertyMapping<TContext>`. Uses compiled expression trees for property access, avoiding reflection on every call. The expression compilation happens once at registration time.

### MappingContextPropagator&lt;TContext&gt;

Internal `IContextPropagator<TContext>` implementation. Collects all `IPropertyMapping<TContext>` instances from DI and delegates `Inject`/`Extract` to them.

## File map

| File | Role |
|---|---|
| `ContextRPropagationExtensions.cs` | `MapProperty` + `Map` DSL entry points on `IContextRegistrationBuilder<T>` |
| `MappingDslBuilders.cs` | DSL builders (`ContextMapBuilder`, `ContextMapPropertyBuilder`, `PropertyRequirement`) |
| `Internal/IPropertyMapping.cs` | Internal interface for single property mapping |
| `Internal/PropertyMapping.cs` | Expression-compiled property accessor and parser |
| `Internal/MappingContextPropagator.cs` | `IContextPropagator<T>` implementation that delegates to property mappings |

## Testing

Strategy-related coverage lives in:

- `tests/ContextR.Propagation.UnitTests` (mapping behavior and guard clauses)
- `tests/ContextR.Propagation.Chunking.UnitTests` (default chunking strategy split/reassembly)
- `tests/ContextR.Propagation.InlineJson.UnitTests` (inline JSON serializer + registration)
- `tests/ContextR.Propagation.Token.UnitTests` (token contracts)
- `tests/ContextR.Propagation.Strategies.IntegrationTests` (integration + functional scenarios on `Microsoft.AspNetCore.TestHost`)
