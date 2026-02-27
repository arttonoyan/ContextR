# ContextR.Propagation

Property-based context propagation for ContextR. This package provides a fluent `MapProperty` API that auto-generates `IContextPropagator<T>` implementations from property-to-key mappings -- no boilerplate serialization code required.

## When to use this package

Use `ContextR.Propagation` when your context classes are simple POCOs with properties that map 1:1 to transport keys (HTTP headers, gRPC metadata, Kafka headers, etc.).

For complex serialization logic -- conditional fields, composite values, encrypted payloads -- implement `IContextPropagator<T>` directly and register it with `UsePropagator<T>()` instead.

## Install

```
dotnet add package ContextR.Propagation
```

Dependencies: `ContextR` (core).

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

## Supported property types

| Type | Parsing strategy |
|---|---|
| `string` | Direct assignment, no parsing |
| Types implementing `IParsable<T>` | `T.TryParse(value, null, out result)` via reflection. Covers `int`, `long`, `double`, `decimal`, `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `bool`, and all other .NET types with `IParsable<T>`. |
| Other types | Fallback to `Convert.ChangeType(value, type)`. Throws on failure. |

### Examples

```csharp
public class RequestContext
{
    public string? CorrelationId { get; set; }  // string → direct
    public Guid RequestId { get; set; }         // Guid → IParsable
    public int RetryCount { get; set; }         // int → IParsable
    public DateTime Timestamp { get; set; }     // DateTime → IParsable
}

ctx.Add<RequestContext>(reg => reg
    .MapProperty(c => c.CorrelationId, "X-Correlation-Id")
    .MapProperty(c => c.RequestId, "X-Request-Id")
    .MapProperty(c => c.RetryCount, "X-Retry-Count")
    .MapProperty(c => c.Timestamp, "X-Timestamp"));
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

If your context type does not meet these requirements, use `UsePropagator<T>()` with a custom `IContextPropagator<T>` implementation that handles construction and population.

## MapProperty vs UsePropagator

`MapProperty` and `UsePropagator` are mutually exclusive for a given context type. The first one registered wins (both use `TryAdd` internally):

```csharp
// MapProperty wins -- registers IContextPropagator<T> as MappingContextPropagator
ctx.Add<CorrelationContext>(reg => reg
    .MapProperty(c => c.TraceId, "X-Trace-Id")
    .UsePropagator<CustomPropagator>());  // no-op, MappingContextPropagator already registered

// UsePropagator wins -- registers IContextPropagator<T> as CustomPropagator
ctx.Add<CorrelationContext>(reg => reg
    .UsePropagator<CustomPropagator>()
    .MapProperty(c => c.TraceId, "X-Trace-Id"));  // adds mapping, but propagator is CustomPropagator
```

In practice, pick one approach per context type and stick with it.

## Custom propagator with other mapping libraries

`UsePropagator<T>()` is the extension point for integrating any serialization strategy. For example, with AutoMapper:

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
    .UsePropagator<AutoMapperPropagator<CorrelationContext>>());
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

Internal interface representing a single property-to-key mapping. Has three members:

- `Key` -- the transport key name
- `GetValue(TContext)` -- reads the property and returns `ToString()` or `null`
- `TrySetValue(TContext, string)` -- parses the string and sets the property, returns `false` on parse failure

### PropertyMapping&lt;TContext, TProperty&gt;

Internal implementation of `IPropertyMapping<TContext>`. Uses compiled expression trees for property access, avoiding reflection on every call. The expression compilation happens once at registration time.

### MappingContextPropagator&lt;TContext&gt;

Internal `IContextPropagator<TContext>` implementation. Collects all `IPropertyMapping<TContext>` instances from DI and delegates `Inject`/`Extract` to them.

## File map

| File | Role |
|---|---|
| `ContextRPropagationExtensions.cs` | `MapProperty` extension method on `IContextRegistrationBuilder<T>` |
| `Internal/IPropertyMapping.cs` | Internal interface for single property mapping |
| `Internal/PropertyMapping.cs` | Expression-compiled property accessor and parser |
| `Internal/MappingContextPropagator.cs` | `IContextPropagator<T>` implementation that delegates to property mappings |
