# Transport gRPC

`ContextR.Transport.Grpc` provides gRPC interceptors and metadata adapters for context propagation.

[Back to Packages Overview](index.md){ .md-button }

## Use This Package When

- gRPC calls must carry context metadata between services
- both client injection and server extraction are needed
- domain-aware propagation is required for gRPC boundaries

## Install

```bash
dotnet add package ContextR.Transport.Grpc
```

## Depends On

- `ContextR`
- `ContextR.Propagation`

## See Also

- [gRPC Integration](../ContextR.Grpc.md)
