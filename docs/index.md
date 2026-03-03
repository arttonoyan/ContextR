# ContextR Docs

## Build once. Propagate everywhere.

ContextR helps you carry operational context (tenant, user, correlation, request metadata) across ASP.NET Core ingress, `HttpClient`, gRPC, async workflows, and background jobs without leaking transport code into business logic.

[Get Started](GettingStarted.md){ .md-button .md-button--primary }
[See Real Samples](samples/index.md){ .md-button }

---

## Why teams use ContextR

- transport-agnostic context propagation model
- snapshot-first business usage for safer async flows
- policy-driven handling for required fields and oversize payloads
- first-hop resolution support for gateway/edge use cases

## Quick navigation

### Start here

- [Why ContextR Was Born](WhyContextR.md)
- [Getting Started](GettingStarted.md)
- [Usage Cookbook](UsageCookbook.md)
- [FAQ](FAQ.md)

### Core architecture

- [Architecture](ARCHITECTURE.md)
- [Context Resolution](ContextR.Resolution.md)
- [Propagation Mapping](ContextR.Propagation.md)

### Transport guides

- [ASP.NET Core](ContextR.AspNetCore.md)
- [HTTP Client](ContextR.Http.md)
- [HTTP Client Handler Scope Deep Dive](HttpClientHandlerScopes.md)
- [gRPC](ContextR.Grpc.md)

### Strategy packages

- [Inline JSON](ContextR.Propagation.InlineJson.md)
- [Chunking](ContextR.Propagation.Chunking.md)
- [Token Contracts](ContextR.Propagation.Token.md)

### Real-world samples

- [Samples Overview](samples/index.md)
- [Multi-tenant SaaS](samples/MultiTenantSaaS.md)
- [Mixed HTTP + gRPC microservices](samples/MicroservicesHttpGrpc.md)
- [Background jobs](samples/BackgroundJobs.md)
- [Gateway ingress resolution](samples/GatewayIngressResolution.md)

!!! tip "No logo yet?"
    The docs theme already supports polished branding (colors, icon, dark mode).  
    You can add a custom logo later without changing content structure.
