# Packages Overview

ContextR is a multi-package ecosystem. Use this section as a package selection compass.

## Ecosystem Model

- **Core**: ambient context, snapshots, domains, and registration primitives.
- **Strategies**: payload and propagation behavior (`Mapping`, `Inline JSON`, `Chunking`, `Token`).
- **Transports**: ingress/egress integrations for ASP.NET Core, `HttpClient`, and gRPC.
- **Utilities and extensions**: package-specific adapters and policy hooks.

## Which Packages Do I Need?

Start with this decision flow:

1. Need ambient context and snapshots only? Install **Core**.
2. Need ingress resolution (gateway/edge first-hop derivation)? Add **Resolution**.
3. Need cross-service propagation? Add **Propagation Base** + **Propagation Mapping**.
4. Need complex property payloads? Add **Propagation Inline JSON**.
5. Need oversize chunk split/reassembly? Add **Propagation Chunking**.
6. Need out-of-band token contracts for large payload references? Add **Propagation Token**.
7. Need ASP.NET Core ingress extraction? Add **Hosting ASP.NET Core**.
8. Need outbound HTTP propagation? Add **Transport HTTP**.
9. Need outbound/inbound gRPC metadata propagation? Add **Transport gRPC**.

## Package Catalog

| Package | Purpose | When to use | Install | Depends on |
|---|---|---|---|---|
| `ContextR` | Core ambient context and snapshot model | Always; foundation for all scenarios | `dotnet add package ContextR` | - |
| `ContextR.Resolution` | First-hop context resolution and precedence policy | Gateway/edge ingress where resolved and propagated values must be merged | `dotnet add package ContextR.Resolution` | `ContextR` |
| `ContextR.Propagation` | Transport-agnostic propagation contracts and policies | Any scenario that propagates context over carriers | `dotnet add package ContextR.Propagation` | `ContextR` |
| `ContextR.Propagation.Mapping` | Property-to-key mapping DSL and default propagator | Most HTTP/gRPC header metadata mapping scenarios | `dotnet add package ContextR.Propagation.Mapping` | `ContextR`, `ContextR.Propagation` |
| `ContextR.Propagation.InlineJson` | JSON payload strategy for complex mapped properties | Lists/arrays/custom class properties in transport metadata | `dotnet add package ContextR.Propagation.InlineJson` | `ContextR`, `ContextR.Propagation` |
| `ContextR.Propagation.Chunking` | Chunk split/reassembly for oversize payloads | Metadata size constraints with `ChunkProperty` behavior | `dotnet add package ContextR.Propagation.Chunking` | `ContextR.Propagation` |
| `ContextR.Propagation.Token` | Token/reference contracts for out-of-band payload storage | Large payload reference contracts and token codecs | `dotnet add package ContextR.Propagation.Token` | `ContextR.Propagation` |
| `ContextR.Hosting.AspNetCore` | ASP.NET Core ingress extraction middleware | Service ingress from HTTP where context arrives in headers | `dotnet add package ContextR.Hosting.AspNetCore` | `ContextR`, `ContextR.Propagation`, `ContextR.Transport.Http` |
| `ContextR.Transport.Http` | `HttpClient` propagation handler and registration APIs | Outbound HTTP propagation through `IHttpClientFactory` | `dotnet add package ContextR.Transport.Http` | `ContextR`, `ContextR.Propagation` |
| `ContextR.Transport.Grpc` | gRPC interceptors and metadata adapters | Outbound/inbound gRPC context propagation | `dotnet add package ContextR.Transport.Grpc` | `ContextR`, `ContextR.Propagation` |

## Package Pages

- [Core](core.md)
- [Resolution](resolution.md)
- [Propagation Base](propagation.md)
- [Propagation Mapping](propagation-mapping.md)
- [Propagation Inline JSON](propagation-inline-json.md)
- [Propagation Chunking](propagation-chunking.md)
- [Propagation Token](propagation-token.md)
- [Hosting ASP.NET Core](hosting-aspnet-core.md)
- [Transport HTTP](transport-http.md)
- [Transport gRPC](transport-grpc.md)
