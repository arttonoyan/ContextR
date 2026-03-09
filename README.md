# ContextR

[![CI](https://github.com/arttonoyan/ContextR/actions/workflows/ci.yml/badge.svg)](https://github.com/arttonoyan/ContextR/actions/workflows/ci.yml)
[![Docs](https://github.com/arttonoyan/ContextR/actions/workflows/docs-pages.yml/badge.svg)](https://github.com/arttonoyan/ContextR/actions/workflows/docs-pages.yml)
[![NuGet Publish](https://github.com/arttonoyan/ContextR/actions/workflows/nuget-publish.yml/badge.svg)](https://github.com/arttonoyan/ContextR/actions/workflows/nuget-publish.yml)
[![NuGet Version](https://img.shields.io/nuget/v/ContextR?label=nuget)](https://www.nuget.org/packages/ContextR)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ContextR?label=downloads)](https://www.nuget.org/packages/ContextR)
[![License: Apache-2.0](https://img.shields.io/github/license/arttonoyan/ContextR)](LICENSE)
[![Docs Site](https://img.shields.io/website?url=https%3A%2F%2Farttonoyan.github.io%2FContextR%2F&label=docs%20site)](https://arttonoyan.github.io/ContextR/)

Context propagation for distributed .NET systems without brittle glue code.

ContextR helps you move request, tenant, user, and operational metadata across async boundaries, HTTP, and gRPC in a consistent and testable way.

## Why This Project Was Born

We run a gateway + microservices architecture where authenticated requests are transformed into typed `UserContext` data (`TenantId`, `UserId`, and related fields).  
That context must continue across HTTP, gRPC, and distributed events as services call each other.

Without a unified model, context handling leaks into business code and turns into repeated boilerplate that is easy to get wrong.  
ContextR was created so engineers do not need to think about propagation mechanics in daily feature work.

Read the full story:

- [Why ContextR Was Born](docs/WhyContextR.md)
- [HTTP Client Handler Scope Deep Dive](docs/HttpClientHandlerScopes.md)

## What You Get

- Snapshot-first model for safer async/background propagation
- Property mapping DSL for HTTP/gRPC keys
- Optional required/optional property contracts
- Payload strategies for complex types (`InlineJson`, `Chunking`, token-ready)
- Domain-aware failure hooks and runtime oversize strategy policy
- Dedicated transport packages for ASP.NET Core, `HttpClient`, and gRPC

## When To Use ContextR

Use ContextR when you need:

- consistent context propagation across service boundaries
- multi-tenant or user/correlation context continuity
- clean separation from `HttpContext` in business logic
- explicit behavior for oversize payloads and parse failures

Avoid ContextR for single-process apps that do not cross async/transport boundaries.

## 60-Second Quick Start

Install:

```bash
dotnet add package ContextR
dotnet add package ContextR.Propagation
dotnet add package ContextR.Propagation.Mapping
dotnet add package ContextR.Hosting.AspNetCore
dotnet add package ContextR.Transport.Http
```

Register:

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<UserContext>(reg => reg
        .MapProperty(c => c.TraceId, "X-Trace-Id")
        .MapProperty(c => c.TenantId, "X-Tenant-Id")
        .MapProperty(c => c.UserId, "X-User-Id")
        .UseAspNetCore()
        .UseGlobalHttpPropagation());
});

public sealed class UserContext
{
    public string? TraceId { get; set; }
    public string? TenantId { get; set; }
    public string? UserId { get; set; }
}
```

Result:

- incoming middleware extracts request headers into context
- outgoing `HttpClient` calls automatically inject mapped headers
- your app code reads context from ContextR abstractions, not transport APIs

## Core Interfaces: Why Both Exist

Engineers often ask why ContextR has both `IContextAccessor` and `IContextSnapshot`.

### `IContextAccessor` (singleton, live view)

- Reads current ambient value from `AsyncLocal` on every call
- Reflects the value that is active right now (middleware writes, `BeginScope()` overrides, cleanup restores/clears)
- Used by integration plumbing that runs at execution time (`HttpClient` handlers, gRPC interceptors, middleware)

Why singleton?  
`IContextAccessor` is stateless. It does not store request data in the instance; it only reads from ambient `AsyncLocal` state. A singleton accessor is safe and lets long-lived infrastructure components read the live context consistently.

### `IContextSnapshot` (scoped, stable view)

- Captures context once for a scope (typically request scope)
- Immutable view for business/application code
- Safe to pass to background work and later re-activate with `BeginScope()`

### Why snapshot cannot replace accessor everywhere

Outbound propagation components need the **currently active** ambient context at the exact moment they run.  
`BeginScope()` works by writing snapshot values into `AsyncLocal`; handlers then read through `IContextAccessor`.  
If handlers depended only on snapshots, they would not know which snapshot should be active in concurrent scenarios (for example parallel `Task.Run`/batch work where multiple snapshots can exist).

Practical rule:

- business logic: prefer `IContextSnapshot`
- infrastructure/integration pipeline: use `IContextAccessor`

## Marketing Message (Site Hero Candidate)

Build once. Propagate everywhere.  
ContextR makes context flow reliable across async code, HTTP, gRPC, and background processing without turning your codebase into header plumbing.

## Documentation Hub

- [Getting Started](docs/GettingStarted.md)
- [Usage Cookbook](docs/UsageCookbook.md)
- [Q&A / FAQ](docs/FAQ.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Context Resolution](docs/ContextR.Resolution.md)
- [Propagation Mapping](docs/ContextR.Propagation.md)
- [Inline JSON Strategy](docs/ContextR.Propagation.InlineJson.md)
- [Chunking Strategy](docs/ContextR.Propagation.Chunking.md)
- [Token Strategy Contracts](docs/ContextR.Propagation.Token.md)
- [ASP.NET Core Transport](docs/ContextR.AspNetCore.md)
- [HTTP Client Transport](docs/ContextR.Http.md)
- [HTTP Client Handler Scope Deep Dive](docs/HttpClientHandlerScopes.md)
- [gRPC Transport](docs/ContextR.Grpc.md)

## GitHub Pages Docs

Once GitHub Pages is enabled for this repository, the site will be published at:

- `https://arttonoyan.github.io/ContextR/`

Local docs preview:

```bash
python -m pip install --upgrade pip
pip install "mkdocs<2" "mkdocs-material<10" pymdown-extensions
mkdocs serve
```

Production build check:

```bash
mkdocs build --strict
```

## Samples

Real-world examples are available under [`samples`](samples):

- [Multi-tenant SaaS propagation](samples/MultiTenantSaaS/README.md)
- [Mixed HTTP + gRPC microservices](samples/MicroservicesHttpGrpc/README.md)
- [Background jobs propagation](samples/BackgroundJobs/README.md)
- [Gateway ingress resolution (JWT -> UserContext)](samples/GatewayIngressResolution/README.md)
- [Replacing `IHttpContextAccessor` usage with snapshot model](samples/HttpContextAccessorReplacement/README.md)
- [JWT vs operational context propagation](samples/JwtAdjunctContext/README.md)

## Package Map

| Package | Purpose |
|---|---|
| `ContextR` | Core ambient context, snapshots, scopes, domains |
| `ContextR.Resolution` | Ingress context resolution contracts/orchestrator/policies (optional package) |
| `ContextR.Propagation` | Propagation contracts + registration APIs |
| `ContextR.Propagation.Mapping` | `MapProperty` and advanced `Map(...)` DSL |
| `ContextR.Propagation.InlineJson` | JSON serializer strategy for complex properties |
| `ContextR.Propagation.Chunking` | Chunk split/reassembly for oversize payloads |
| `ContextR.Propagation.Token` | Token/reference propagation contracts |
| `ContextR.Hosting.AspNetCore` | Incoming ASP.NET Core extraction middleware |
| `ContextR.Transport.Http` | Outgoing `HttpClient` propagation handler |
| `ContextR.Transport.Grpc` | gRPC client/server propagation interceptors |

### Resolution Registration Note

`ContextR.Resolution` stays optional by design.  
If you use `UseResolver(...)` or `UseResolutionPolicy(...)`, resolution services are auto-registered for you.

Use `AddContextRResolution()` only for advanced cases where you need orchestrator/policy services without configuring resolver/policy registrations yet.

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on branch naming, commit conventions, and the PR process.

- [Contributing Guide](CONTRIBUTING.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)
- [Security Policy](SECURITY.md)
- [Changelog](CHANGELOG.md)

## Design Principles

- explicit over implicit behavior
- transport-agnostic core, transport-specific extensions
- safe defaults with configurable policy where needed
- testable abstractions first (domain and execution-scope aware)

## Status

ContextR is actively evolving. Breaking changes may occur while architecture and package boundaries are refined.
