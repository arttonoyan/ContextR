# Integration

Integration docs explain how to apply ContextR in concrete runtime boundaries.

## Integration Areas

- [ASP.NET Core Ingress](../ContextR.AspNetCore.md)
- [HTTP Client Egress](../ContextR.Http.md)
- [gRPC](../ContextR.Grpc.md)
- [Async and Background Work](../UsageCookbook.md)
- [HTTP Handler Scopes](../HttpClientHandlerScopes.md)

## Typical Integration Order

1. Define context contract and propagation mapping.
2. Enable ingress extraction for edge services.
3. Enable outbound propagation for HTTP and/or gRPC clients.
4. Validate parallel and background execution paths with snapshots.
