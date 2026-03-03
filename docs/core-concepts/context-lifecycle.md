# Context Lifecycle: Ambient vs Snapshot

ContextR intentionally provides two access models. Each solves a different runtime requirement.

## Ambient Context

Ambient context is accessed through `IContextAccessor` and `IContextWriter`. It reads and writes live `AsyncLocal` state and is appropriate for integration boundaries such as middleware, handlers, and interceptors.

## Snapshot Context

`IContextSnapshot` captures context values at a point in time and provides a stable view for application and domain services. This avoids accidental coupling to mutable ambient state during asynchronous operations.

## Practical Rule

- Use `IContextSnapshot` in business/application code.
- Use `IContextAccessor` and `IContextWriter` in infrastructure code.

## Scope Bridging

`BeginScope()` applies snapshot values to ambient state for the current execution flow and restores prior values on dispose. This is the supported bridge for background jobs and fan-out patterns.

## Related Pages

- [Architecture](../ARCHITECTURE.md)
- [HTTP Client Handler Scope Deep Dive](../HttpClientHandlerScopes.md)
- [Background Jobs Sample](../samples/BackgroundJobs.md)
