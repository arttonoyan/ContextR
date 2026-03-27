# ContextR.Transport.Http

HTTP client context propagation for ContextR. This package provides a `DelegatingHandler` that automatically injects ambient context values into outgoing `HttpClient` request headers using the registered `IContextPropagator<T>`.

## When to use this package

Use `ContextR.Transport.Http` when your application makes outgoing HTTP calls via `IHttpClientFactory` and you want context (correlation IDs, tenant info, feature flags) to propagate automatically to downstream services.

## Install

```
dotnet add package ContextR.Transport.Http
```

Dependencies: `ContextR` (core), `ContextR.Propagation` (for `IContextPropagator<T>`), `Microsoft.Extensions.Http`.

For a dedicated explanation of handler/request scope mismatch and recommended patterns, see [HTTP Client Handler Scopes Deep Dive](HttpClientHandlerScopes.md).

## Two registration modes

### Global propagation (recommended for most apps)

Applies to **all** `HttpClient` instances created by `IHttpClientFactory`:

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<CorrelationContext>(reg => reg
        .MapProperty(c => c.TraceId, "X-Trace-Id")
        .UseGlobalHttpPropagation());
});
```

Under the hood, `UseGlobalHttpPropagation()` calls `ConfigureHttpClientDefaults` to add the handler to every client pipeline. This means named and typed clients both get context propagation without any additional configuration.

### Per-client propagation

Applies to **specific** named or typed `HttpClient` instances:

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<CorrelationContext>(reg => reg
        .MapProperty(c => c.TraceId, "X-Trace-Id"));
    // No UseGlobalHttpPropagation -- manual control
});

// Only this client gets context propagation
builder.Services.AddHttpClient("payment-api")
    .AddContextRHandler<CorrelationContext>();

// This client does not propagate context
builder.Services.AddHttpClient("public-api");
```

`AddContextRHandler<T>()` is an extension method on `IHttpClientBuilder` in the `Microsoft.Extensions.DependencyInjection` namespace. It adds a `ContextPropagationHandler<T>` to that specific client's handler pipeline.

## How it works

### ContextPropagationHandler&lt;TContext&gt;

A `DelegatingHandler` that runs in the `HttpClient` message handler pipeline:

```
SendAsync called
  → Read ambient context via IContextAccessor.GetContext<TContext>()
  → If context is not null:
      propagator.Inject(context, request.Headers, (h, k, v) => h.TryAddWithoutValidation(k, v))
  → base.SendAsync(request, cancellationToken)
```

The handler reads the **live ambient context** from `AsyncLocal` (via `IContextAccessor`), not from a snapshot. This ensures it always picks up the latest context value, including values written by middleware or scopes.

### Handler lifetime

The handler is registered as **scoped** in DI. `IHttpClientFactory` creates a new scope for each handler pipeline, so each HTTP request gets a fresh handler instance. This is important because `DelegatingHandler` is not thread-safe -- it must not be shared across concurrent requests.

### Why TryAddWithoutValidation?

The handler uses `headers.TryAddWithoutValidation(key, value)` instead of `headers.Add(key, value)`. This avoids `FormatException` for header values that don't conform to RFC 7230 validation rules. Custom headers with arbitrary string values (correlation IDs, tenant slugs) may contain characters that strict validation would reject.

## Domain-aware propagation

When context is registered within a domain, `UseGlobalHttpPropagation()` captures the domain at configuration time and passes it to the handler. The handler then reads context from the specific domain slot:

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.AddDomain("web-api", d => d.Add<CorrelationContext>(reg => reg
        .MapProperty(c => c.TraceId, "X-Trace-Id")
        .UseGlobalHttpPropagation()));
});
```

In this configuration, the handler calls `_accessor.GetContext<CorrelationContext>("web-api")` instead of the parameterless `GetContext<CorrelationContext>()`. This ensures domain isolation -- the handler only reads from the domain it was configured for.

The domain capture happens via closure:

```
UseGlobalHttpPropagation() called
  → var domain = builder.Domain     // captured at config time
  → registers handler factory that creates ContextPropagationHandler(accessor, propagator, domain)
```

## Multiple context types

Register multiple context types with their own propagation:

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<CorrelationContext>(reg => reg
        .MapProperty(c => c.TraceId, "X-Trace-Id")
        .UseGlobalHttpPropagation());

    ctx.Add<TenantContext>(reg => reg
        .MapProperty(c => c.TenantId, "X-Tenant-Id")
        .UseGlobalHttpPropagation());
});
```

Each context type gets its own `ContextPropagationHandler<T>` in the pipeline. The handlers are independent -- if `CorrelationContext` is set but `TenantContext` is not, only correlation headers are injected.

## Interaction with snapshots

The handler reads from `IContextAccessor` (live `AsyncLocal`), not from `IContextSnapshot`. This means:

- If middleware sets context via `IContextWriter`, the handler picks it up
- If a scope is active (via `snapshot.BeginScope()`), the handler reads the scoped values
- If no context is set, the handler does nothing (no headers injected, no errors)

For background jobs or `Task.Run` scenarios, ensure a scope is active before making HTTP calls:

```csharp
var snapshot = _accessor.CaptureSnapshot();

_ = Task.Run(async () =>
{
    using (snapshot.BeginScope())
    {
        // HttpClient calls made here will include context headers
        await _httpClient.GetAsync("/api/downstream");
    }
});
```

## Complex payloads and header limits

For mapped complex properties (`List<T>`, arrays, custom classes), combine HTTP transport with a payload strategy package:

```csharp
ctx.Add<UserContext>(reg => reg
    .UseInlineJsonPayloads<UserContext>(o =>
    {
        o.MaxPayloadBytes = 4096;
        o.OversizeBehavior = ContextOversizeBehavior.FailFast;
    })
    .MapProperty(c => c.Roles, "X-Roles")
    .UseGlobalHttpPropagation());
```

HTTP infrastructure has practical header-size limits. Keep payloads small, and use token/reference strategy (`ContextR.Propagation.Token`) for large values.

## File map

| File | Role |
|---|---|
| `ContextPropagationHandler.cs` | `DelegatingHandler` that injects context into outgoing HTTP request headers |
| `Extensions/ContextRHttpRegistrationExtensions.cs` | `UseGlobalHttpPropagation()` extension on `IContextTypeBuilder<T>` |
| `Extensions/ContextRHttpClientBuilderExtensions.cs` | `AddContextRHandler<T>()` extension on `IHttpClientBuilder` |
