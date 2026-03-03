# Propagation

Propagation in ContextR is transport-agnostic and policy-driven. This section covers mapping, payload behavior, and failure handling.

## Topics

- [Mapping and Propagator Model](../ContextR.Propagation.md)
- [Inline JSON Strategy](../ContextR.Propagation.InlineJson.md)
- [Chunking Strategy](../ContextR.Propagation.Chunking.md)
- [Token Contracts](../ContextR.Propagation.Token.md)

## Design Notes

- Keep context contracts explicit and versioned.
- Set required and optional fields intentionally.
- Define oversize behavior per property when contracts differ in criticality.
- Treat token fallback as an explicit distributed-system decision.
