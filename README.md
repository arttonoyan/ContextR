# ContextR

Context propagation for distributed .NET systems without brittle glue code.

ContextR helps you move request, tenant, user, and operational metadata across async boundaries, HTTP, and gRPC in a consistent and testable way.

## Why This Project Was Born

In our system, a gateway fronts multiple standalone microservices running in an internal network.

The gateway supports multiple authentication schemes. After authentication succeeds, it transforms the token into a typed `UserContext` (for example `TenantId`, `UserId`, and related fields) and forwards that context to downstream services.

The challenge is that downstream services also call each other over HTTP, gRPC, and distributed events.  
So `UserContext` must propagate consistently across all communication layers, not only at the gateway edge.

Over time, engineers started spending too much effort on context plumbing:

- how to retrieve context safely
- how to pass it across async boundaries
- how to propagate it through transport-specific handlers/interceptors

Context concerns leaked into business logic, created repetitive boilerplate, and increased the chance of mistakes.

Our goal became simple: engineers should not think about context propagation. It should just work.

As implementation evolved, we discovered deeper platform challenges:

- `HttpClient` handler scope behavior and pipeline reuse (see [HTTP Client Handler Scope Deep Dive](docs/HttpClientHandlerScopes.md))
- singleton infrastructure components that still need contextual data
- subtle `AsyncLocal` behavior across async flows and service lifetimes

Those lessons made it clear we needed one structured, unified model for context propagation across the entire platform.

ContextR was born from that need.

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
- [Propagation Mapping](docs/ContextR.Propagation.md)
- [Inline JSON Strategy](docs/ContextR.Propagation.InlineJson.md)
- [Chunking Strategy](docs/ContextR.Propagation.Chunking.md)
- [Token Strategy Contracts](docs/ContextR.Propagation.Token.md)
- [ASP.NET Core Transport](docs/ContextR.AspNetCore.md)
- [HTTP Client Transport](docs/ContextR.Http.md)
- [HTTP Client Handler Scope Deep Dive](docs/HttpClientHandlerScopes.md)
- [gRPC Transport](docs/ContextR.Grpc.md)

## Samples

Real-world examples are available under [`samples`](samples):

- [Multi-tenant SaaS propagation](samples/MultiTenantSaaS/README.md)
- [Mixed HTTP + gRPC microservices](samples/MicroservicesHttpGrpc/README.md)
- [Background jobs propagation](samples/BackgroundJobs/README.md)
- [Replacing `IHttpContextAccessor` usage with snapshot model](samples/HttpContextAccessorReplacement/README.md)
- [JWT vs operational context propagation](samples/JwtAdjunctContext/README.md)

## Package Map

| Package | Purpose |
|---|---|
| `ContextR` | Core ambient context, snapshots, scopes, domains |
| `ContextR.Propagation` | Propagation contracts + registration APIs |
| `ContextR.Propagation.Mapping` | `MapProperty` and advanced `Map(...)` DSL |
| `ContextR.Propagation.InlineJson` | JSON serializer strategy for complex properties |
| `ContextR.Propagation.Chunking` | Chunk split/reassembly for oversize payloads |
| `ContextR.Propagation.Token` | Token/reference propagation contracts |
| `ContextR.Hosting.AspNetCore` | Incoming ASP.NET Core extraction middleware |
| `ContextR.Transport.Http` | Outgoing `HttpClient` propagation handler |
| `ContextR.Transport.Grpc` | gRPC client/server propagation interceptors |

## Design Principles

- explicit over implicit behavior
- transport-agnostic core, transport-specific extensions
- safe defaults with configurable policy where needed
- testable abstractions first (domain and execution-scope aware)

## Status

ContextR is actively evolving. Breaking changes may occur while architecture and package boundaries are refined.
