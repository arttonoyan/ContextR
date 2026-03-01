# Q&A / FAQ

## Why not use `IHttpContextAccessor` everywhere?

`IHttpContextAccessor` is tied to ASP.NET request lifecycle and web concerns.  
ContextR gives you explicit context contracts that also work in background jobs, message consumers, and mixed transport systems.

## Is ContextR replacing `AsyncLocal`?

No. ContextR is built on top of `AsyncLocal`, but adds snapshots, scopes, domain isolation, and propagation tooling so behavior is predictable and testable.

## Snapshot vs accessor: which one should app code use?

Use `IContextSnapshot` in application/business code.  
Use `IContextAccessor` and `IContextWriter` in infrastructure and integration edges.

`IContextSnapshot` is a stable, captured view; `IContextAccessor` is a live ambient read from `AsyncLocal`.

## Why is `IContextAccessor` singleton?

Because the accessor itself is stateless.  
It does not hold request data in instance fields; it reads ambient state from `AsyncLocal` each time you call it.

A singleton accessor is required so long-lived integration components (for example `HttpClient` propagation handlers and interceptors) can always read the active context at execution time.

## Why can't we use only snapshots?

Snapshots are immutable captured values, which is ideal for business logic.  
Propagation infrastructure needs the *currently active* ambient context when it runs.

`BeginScope()` writes snapshot values into `AsyncLocal`. Handlers then read via `IContextAccessor`.  
If handlers used snapshots directly, they would not know which snapshot to use in concurrent scenarios where multiple snapshots may exist.

## Can ContextR replace JWT/auth tokens?

Not directly. JWT is for identity/authentication and trust boundaries.  
ContextR is for operational context propagation (tenant, correlation, request metadata, workflow hints).  
Use them together:

- JWT for identity claims and authorization
- ContextR for cross-service operational context

## Can I map list/array/custom class properties?

Yes, with `ContextR.Propagation.InlineJson`.  
Add `ContextR.Propagation.Chunking` if you also need `ChunkProperty` behavior for large payloads.

## What about HTTP/gRPC header size limits?

They are real and infrastructure-dependent.  
Use transport policy (`MaxPayloadBytes`) plus oversize behavior to avoid silent failures.

## What happens on propagation failures?

By default, required/malformed/oversize failures throw deterministic exceptions.  
You can customize with `OnPropagationFailure(...)` and choose to `Throw`, `SkipProperty`, or `SkipContext`.

## Is behavior domain-aware?

Yes. Failure handlers and strategy policies can be registered per domain, with default fallback behavior.

## Can I plug in my own strategy logic?

Yes:

- custom `IContextPropagator<TContext>`
- custom payload serializer and transport policy
- runtime strategy policy (`UseStrategyPolicy`)
- token/store implementations via `ContextR.Propagation.Token`

## Where should I start after reading this?

1. [Getting Started](GettingStarted.md)  
2. [Usage Cookbook](UsageCookbook.md)  
3. [Samples](../samples/README.md)
