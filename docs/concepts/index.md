# Concepts

Concepts explain ContextR's mental model independent of any specific package.

## Core Ideas

- **Ambient context** is live, transport-facing context stored in `AsyncLocal`.
- **Snapshots** are stable captured values for business and async workflows.
- **Resolution** handles first-hop context derivation and trust-boundary precedence.
- **Propagation** moves context over transport boundaries through explicit mappings and policies.

## Read In Order

1. [Context Lifecycle](../core-concepts/context-lifecycle.md)
2. [Architecture](../ARCHITECTURE.md)
3. [Resolution Model](../ContextR.Resolution.md)
