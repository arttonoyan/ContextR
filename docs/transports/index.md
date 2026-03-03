# Transports

ContextR integrates with ingress and egress transports while preserving one context model.

## Topics

- [ASP.NET Core Ingress](../ContextR.AspNetCore.md)
- [HTTP Client Propagation](../ContextR.Http.md)
- [gRPC Propagation](../ContextR.Grpc.md)
- [HTTP Handler Scope Deep Dive](../HttpClientHandlerScopes.md)

## Integration Order

1. Configure context contract and propagation mapping.
2. Enable ingress extraction where requests enter the service.
3. Enable outbound propagation for HTTP and/or gRPC clients.
4. Validate behavior under parallel and background workloads.
