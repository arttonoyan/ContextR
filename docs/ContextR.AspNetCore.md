# ContextR.Hosting.AspNetCore

ASP.NET Core middleware for ContextR. This package extracts context from incoming HTTP request headers and writes it into the ambient `AsyncLocal` storage, making it available to all downstream code via `IContextAccessor`, `IContextSnapshot`, and `IContextWriter`.

## When to use this package

Use `ContextR.Hosting.AspNetCore` when your ASP.NET Core application receives context values (correlation IDs, tenant identifiers, feature flags) as HTTP headers and you want them available as typed context objects throughout the request pipeline.

## Install

```
dotnet add package ContextR.Hosting.AspNetCore
```

Dependencies: `ContextR` (core), `ContextR.Propagation` (for `IContextPropagator<T>`), `ContextR.Transport.Http`, `Microsoft.AspNetCore.App` (framework reference).

## Quick start

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<CorrelationContext>(reg => reg
        .MapProperty(c => c.TraceId, "X-Trace-Id")
        .MapProperty(c => c.SpanId, "X-Span-Id")
        .UseAspNetCore());
});

public class CorrelationContext
{
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
}
```

With this configuration, every incoming HTTP request with `X-Trace-Id` or `X-Span-Id` headers will have a `CorrelationContext` automatically available in the ambient context.

## How it works

### Registration via IStartupFilter

`UseAspNetCore()` registers an `IStartupFilter` that adds the context extraction middleware at the **beginning** of the request pipeline. This means context is available before any other middleware, controllers, or filters execute.

```
UseAspNetCore() called
  → registers IStartupFilter = ContextStartupFilter<TContext>(domain)
  → at app startup, IStartupFilter.Configure() inserts:
      app.UseMiddleware<ContextMiddleware<TContext>>()
    before all other middleware
```

Using `IStartupFilter` instead of requiring `app.UseMiddleware<T>()` in `Program.cs` has two advantages:

1. **Automatic** -- no explicit middleware registration needed in the application startup
2. **Early** -- the middleware is inserted before user-configured middleware, ensuring context is available everywhere

### ContextMiddleware&lt;TContext&gt;

The middleware runs on every request:

```
InvokeAsync(HttpContext, IContextPropagator<TContext>, IContextWriter, IServiceProvider, ...)
  → context = propagator.Extract(httpContext.Request.Headers, headerGetter)
  → if context is not null:
      writer.SetContext(context)      // writes to AsyncLocal
  → await _next(httpContext)          // continue pipeline
```

The middleware resolves `IContextPropagator<TContext>` and `IContextWriter` from DI per request (method injection via ASP.NET Core's middleware convention). The propagator's `Extract` method reads header values from `IHeaderDictionary` using a static getter delegate:

```csharp
static (headers, key) => headers.TryGetValue(key, out var values) ? (string?)values : null
```

### What happens when headers are missing?

If the propagator's `Extract` method returns `null` (no matching headers found), the middleware does nothing -- no context is written, and the request continues normally. Downstream code calling `GetContext<T>()` will receive `null`.

If only some headers are present (partial extraction), the behavior depends on the propagator implementation. `MappingContextPropagator` returns a context object with only the found properties set -- `null` properties for missing headers.

## Ingress enforcement (optional)

`UseAspNetCore(...)` supports ingress enforcement with fluent options:

```csharp
ctx.Add<UserContext>(reg => reg
    .Map(m => m
        .ByConvention()
        .Property(c => c.TenantId, "X-Tenant-Id")
        .Property(c => c.TraceId, "X-Trace-Id"))
    .UseAspNetCore(o => o.Enforcement(e =>
    {
        e.Mode = ContextIngressEnforcementMode.FailRequest;
        e.OnFailure = failure => ContextIngressFailureDecision.Fail(
            statusCode: 400,
            message: "Required context is missing.");
    })));
```

Available modes:

- `Disabled`: extraction-only behavior (default)
- `ObserveOnly`: invoke failure callback/logging but continue request
- `FailRequest`: short-circuit request unless callback overrides decision

You can also provide fallback context creation:

```csharp
.UseAspNetCore(o => o.Enforcement(e =>
{
    e.Mode = ContextIngressEnforcementMode.FailRequest;
    e.FallbackContextFactory = http => new UserContext
    {
        TenantId = "default-tenant",
        TraceId = http.TraceIdentifier
    };
}))
```

### DI-aware fluent configuration

When configuration needs services (for example a logger), use the DI-aware overload:

```csharp
.UseAspNetCore((sp, o) =>
{
    var logger = sp.GetRequiredService<ILogger<Startup>>();
    o.Enforcement(e =>
    {
        e.Mode = ContextIngressEnforcementMode.ObserveOnly;
        e.OnFailure = failure =>
        {
            logger.LogWarning("Context enforcement failure: {Reason}", failure.Reason);
            return ContextIngressFailureDecision.Continue();
        };
    });
})
```

## Domain-aware extraction

When context is registered within a domain, `UseAspNetCore()` captures the domain and passes it to the middleware. The middleware then writes context to the specific domain slot:

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.AddDomain("web-api", d => d.Add<CorrelationContext>(reg => reg
        .MapProperty(c => c.TraceId, "X-Trace-Id")
        .UseAspNetCore()));
});
```

In this configuration, the middleware calls `writer.SetContext("web-api", context)` instead of `writer.SetContext(context)`. Downstream code must use `GetContext<CorrelationContext>("web-api")` to read the value.

### Domain capture

The domain is captured at configuration time via closure:

```
UseAspNetCore() called
  → var domain = builder.Domain    // null for default, "web-api" for domain registration
  → registers ContextStartupFilter<T>(domain)
  → middleware receives domain via constructor
```

When `domain` is `null` (default registration), the middleware calls `app.UseMiddleware<ContextMiddleware<T>>()` without the domain argument, and `SetContext(context)` writes to the default slot.

When `domain` is not `null`, the middleware calls `app.UseMiddleware<ContextMiddleware<T>>(domain)`, and `SetContext(domain, context)` writes to the domain-specific slot.

## Multiple context types

Register multiple context types, each with its own middleware:

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<CorrelationContext>(reg => reg
        .MapProperty(c => c.TraceId, "X-Trace-Id")
        .UseAspNetCore());

    ctx.Add<TenantContext>(reg => reg
        .MapProperty(c => c.TenantId, "X-Tenant-Id")
        .UseAspNetCore());
});
```

Each `UseAspNetCore()` call registers a separate `IStartupFilter`, and each filter adds its own `ContextMiddleware<T>`. The middleware instances run independently -- each extracts its own context type from headers.

## Combined with HTTP propagation

The most common pattern combines incoming extraction with outgoing propagation:

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<CorrelationContext>(reg => reg
        .MapProperty(c => c.TraceId, "X-Trace-Id")
        .MapProperty(c => c.SpanId, "X-Span-Id")
        .UseAspNetCore()                // extract from incoming requests
        .UseGlobalHttpPropagation());   // inject into outgoing HttpClient calls
});
```

The flow for a request:

```
Incoming HTTP request with X-Trace-Id: abc123
  → ContextMiddleware extracts CorrelationContext { TraceId = "abc123" }
  → Writes to AsyncLocal via IContextWriter
  → Controller/service makes HttpClient call
  → ContextPropagationHandler reads from AsyncLocal via IContextAccessor
  → Injects X-Trace-Id: abc123 into outgoing request headers
```

## Interaction with IContextSnapshot

The middleware writes to `AsyncLocal` via `IContextWriter.SetContext()`. The scoped `IContextSnapshot` is created when the DI scope is resolved (typically at the start of the request). Because middleware runs within the same scope, context set by middleware is captured in the snapshot.

However, the timing matters:

- If `IContextSnapshot` is resolved **after** middleware runs (normal case), it captures the context
- If `IContextSnapshot` is resolved **before** middleware (unusual, e.g., in another `IStartupFilter`), the snapshot may be empty

In practice, the standard ASP.NET Core DI scope creation happens before middleware, so `IContextSnapshot` is resolved lazily and captures the correct values.

## File map

| File | Role |
|---|---|
| `Internal/ContextMiddleware.cs` | Middleware that extracts context from `HttpContext.Request.Headers` |
| `Internal/ContextStartupFilter.cs` | `IStartupFilter` that registers middleware at pipeline start |
| `Extensions/ContextRAspNetCoreRegistrationExtensions.cs` | `UseAspNetCore()` extension on `IContextRegistrationBuilder<T>` |
