# Resolution

`ContextR.Resolution` adds first-hop context derivation and resolution policy orchestration.

[Back to Packages Overview](index.md){ .md-button }

## Use This Package When

- ingress services must derive context from trusted sources (for example JWT claims)
- resolved and propagated values must be merged by boundary-aware precedence
- gateway and edge services require explicit trust-boundary logic

## Install

```bash
dotnet add package ContextR.Resolution
```

## Depends On

- `ContextR`

## See Also

- [Resolution Details](../ContextR.Resolution.md)
- [Gateway Ingress Resolution Sample](../samples/GatewayIngressResolution.md)
