# Core Concepts

ContextR separates context handling into explicit layers so behavior remains predictable at scale.

## Concept Model

- **Ambient context**: live values stored in `AsyncLocal`, exposed by `IContextAccessor` and `IContextWriter`.
- **Snapshots**: immutable captured values, exposed by `IContextSnapshot`, recommended for application code.
- **Domains**: isolated logical slots for the same context type.
- **Resolution**: first-hop context derivation and precedence policy at ingress boundaries.

## Read In Order

1. [Context Lifecycle: Ambient vs Snapshot](context-lifecycle.md)
2. [Context Resolution](../ContextR.Resolution.md)
3. [Architecture Overview](../ARCHITECTURE.md)
