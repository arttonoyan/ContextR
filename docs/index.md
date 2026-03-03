# ContextR

ContextR is a .NET infrastructure library for reliable context propagation in distributed systems.

It provides a consistent model for carrying operational context (for example tenant, user, trace, and request metadata) across:

- ASP.NET Core ingress
- outgoing `HttpClient` calls
- gRPC client and server flows
- asynchronous workflows
- background jobs

[Start with Getting Started](GettingStarted.md){ .md-button .md-button--primary }
[Browse Architecture](ARCHITECTURE.md){ .md-button }

## Problem ContextR Solves

In production systems, context handling often becomes fragmented across middleware, handlers, interceptors, and application services. Teams usually see:

- repeated transport-specific plumbing
- inconsistent trust and precedence rules at service boundaries
- fragile async behavior and hidden context loss
- duplicated parsing and validation logic

ContextR centralizes these responsibilities with explicit context contracts, transport integrations, and policy-driven propagation behavior.

## Who ContextR Is For

ContextR is designed for senior backend engineers and platform teams who maintain multi-service .NET systems and need deterministic context behavior across HTTP, gRPC, and async execution paths.

Typical adopters include teams building:

- internal platform foundations
- multi-tenant service meshes
- API gateway plus downstream service topologies
- mixed synchronous and background processing pipelines

## When To Use ContextR

Use ContextR when you need one or more of the following:

- typed context contracts shared across services
- ingress extraction with consistent downstream propagation
- clear separation between live ambient reads and stable business snapshots
- domain-aware context isolation
- policy control for missing, malformed, or oversize propagated values

## When Not To Use ContextR

ContextR is not a replacement for identity, authentication, or authorization systems.

Do not use ContextR:

- as a substitute for JWT, OAuth, or access control decisions
- for large document transport where payload data should be in message bodies
- when a single-process application can use direct method parameters without infrastructure overhead

## Documentation Map

- [Introduction](introduction/index.md) - adoption path, audience, and usage boundaries
- [Core Concepts](core-concepts/index.md) - context model, lifecycle, and resolution
- [Architecture](architecture/index.md) - internals, storage model, and design decisions
- [Propagation](propagation/index.md) - mapping, payload strategies, and failure policy
- [Transports](transports/index.md) - ASP.NET Core, HTTP client, and gRPC integration
- [Advanced](advanced/index.md) - cookbook patterns, background workflows, and FAQ
- [Samples](samples/index.md) - complete production-oriented scenarios
- [API Reference](api-reference/index.md) - package-level reference pages
