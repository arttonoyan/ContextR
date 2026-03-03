# Propagation Base

`ContextR.Propagation` defines transport-agnostic propagation contracts, payload policies, and failure handling abstractions.

[Back to Packages Overview](index.md){ .md-button }

## Use This Package When

- you need to inject/extract context through transport carriers
- you need custom propagators or policy hooks
- transport packages must share one propagation contract

## Install

```bash
dotnet add package ContextR.Propagation
```

## Depends On

- `ContextR`

## See Also

- [Propagation Base Details](../ContextR.Propagation.md)
