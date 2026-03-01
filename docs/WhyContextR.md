# Why ContextR Was Born

## The real system context

Our platform uses a gateway in front of multiple standalone microservices running in an internal network.

The gateway supports multiple authentication schemes. After successful authentication, it transforms the token into a typed `UserContext` (for example `TenantId`, `UserId`, and other operational fields) and forwards that context to downstream services.

## Where the problem appears

Downstream services do not only handle incoming gateway traffic. They also communicate with each other through:

- HTTP
- gRPC
- distributed events

So the same context must propagate correctly across all communication layers, not only once at the gateway edge.

## What went wrong with ad-hoc approaches

As the system grew, engineers spent too much time on context plumbing:

- how to retrieve context safely
- how to pass context across async boundaries
- how to map/inject/extract context per transport

Context concerns leaked into business logic. Boilerplate expanded, and mistakes became more likely.

## The design goal

The goal became simple:

Engineers should not think about context propagation. It should just work.

## Why this became a larger initiative

Implementation exposed deeper platform-level challenges:

- `HttpClient` handler scope vs request scope behavior
- singleton infrastructure components that still need contextual values
- subtle `AsyncLocal` behavior across async flows and service lifetimes

These are not isolated edge cases. They are core constraints for distributed .NET systems.

## The resulting direction

ContextR was created as a structured, unified model for context propagation:

- explicit context contracts
- consistent transport integration
- stable business-code experience with snapshots
- live ambient reads for integration plumbing
- deterministic failure and payload policies

This project was born from practical pain in a real production architecture, not from theory.

## Related deep dives

- [HTTP Client Handler Scopes Deep Dive](HttpClientHandlerScopes.md)
- [Q&A / FAQ](FAQ.md)
- [Getting Started](GettingStarted.md)
