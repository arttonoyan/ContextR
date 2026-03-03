# ContextR

ContextR is a .NET infrastructure library for reliable context propagation in distributed systems.

It provides a consistent model for carrying operational context (for example tenant, user, trace, and request metadata) across:

- ASP.NET Core ingress
- outgoing `HttpClient` calls
- gRPC client and server flows
- asynchronous workflows
- background jobs

[Get Started](getting-started/index.md){ .md-button .md-button--primary }
[Packages Compass](packages/index.md){ .md-button }

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

## Key Capabilities

- snapshot-first access model for safer async and background flows
- transport-agnostic propagation model with explicit mapping contracts
- ingress resolution support for gateway and edge boundaries
- policy-driven handling for required fields and oversize payloads
- domain-aware isolation for multi-boundary service topologies

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

## Quick Start Paths

- [Getting Started](getting-started/index.md) for first implementation
- [Concepts](concepts/index.md) for the ContextR mental model
- [Integration](integration/index.md) for transport-specific setup
- [Packages](packages/index.md) for ecosystem selection and dependency guidance
- [Samples](samples/index.md) for production-style reference scenarios
