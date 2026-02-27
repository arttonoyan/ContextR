# ContextR

A lightweight, generic library for propagating ambient execution context across async flows in .NET applications. ContextR provides `AsyncLocal`-based storage, immutable snapshots, scope-based activation, and domain isolation -- giving you full control over how context values travel through your application.

## Why ContextR?

In async .NET applications, passing contextual data (user identity, tenant, correlation IDs, feature flags) through every method signature is tedious and error-prone. `AsyncLocal<T>` solves part of this -- it makes data ambient so any code on the same `ExecutionContext` can read it without explicit parameters.

But raw `AsyncLocal` has sharp edges:

- `Task.Run` gets a copy that can go stale when the parent clears the value
- Background jobs and `ThreadPool.QueueUserWorkItem` may not capture `ExecutionContext` at all
- There is no built-in mechanism to temporarily override and restore context values

ContextR wraps `AsyncLocal` with a clean, tested API that handles these scenarios through **snapshots** and **scopes**.

## Core mental model

**Bank account analogy:**

| | Bank analogy | ContextR |
|---|---|---|
| `IContextAccessor` | **Live bank balance** -- reflecting every deposit and withdrawal immediately | Reads from `AsyncLocal`, reflects every `SetContext()` and `BeginScope()` |
| `IContextSnapshot` | **Bank statement** -- the balance at a fixed point in time, always the same no matter when you read it | Captured once, never changes |
| `IContextWriter` | **Making a deposit or withdrawal** | Writes to `AsyncLocal` directly |
| `BeginScope()` | Temporarily applying statement values to the live balance view, then restoring the original when done | Writes snapshot values into `AsyncLocal`, restores previous state on dispose |
| **Domain** | **Different accounts at the same bank** -- checking vs savings, each with its own balance | Independent storage slots identified by a string domain name |

Your service code should use the **statement** (snapshot). Reserve the **live balance** (accessor) for infrastructure that must read the current ambient state.

## Quick start

### Install

```
dotnet add package ContextR
```

### Register services

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<UserContext>();
    ctx.Add<TenantContext>();
});
```

### Read and write context

```csharp
public class OrderService
{
    private readonly IContextAccessor _accessor;
    private readonly IContextWriter _writer;

    public OrderService(IContextAccessor accessor, IContextWriter writer)
    {
        _accessor = accessor;
        _writer = writer;
    }

    public void Process()
    {
        _writer.SetContext(new UserContext("alice"));
        var user = _accessor.GetContext<UserContext>();
        Console.WriteLine(user?.UserId); // "alice"
    }
}
```

### Snapshots and scopes

```csharp
// Capture current ambient state into an immutable snapshot
var snapshot = _accessor.CreateSnapshot();

// Or create a snapshot from a specific value (no AsyncLocal mutation)
var snapshot = _accessor.CreateSnapshot(new UserContext("bob"));

// Activate the snapshot -- writes values into AsyncLocal
using (snapshot.BeginScope())
{
    // All code here sees snapshot values via IContextAccessor
    var user = _accessor.GetContext<UserContext>(); // "bob"
}
// Previous ambient state is automatically restored
```

## Key interfaces

| Interface | Lifetime | Purpose |
|---|---|---|
| `IContextAccessor` | Singleton | Reads live ambient context from `AsyncLocal`. Use in infrastructure code. |
| `IContextWriter` | Singleton | Writes ambient context to `AsyncLocal`. Use to set initial values. |
| `IContextSnapshot` | Scoped | Immutable captured state. **Recommended for most service code.** Read values directly or activate with `BeginScope()`. |
| `IContextBuilder` | (config-time) | Fluent builder for registering context types and domains. |

## Generic context model

ContextR is **fully generic**. Any `class` can be a context type. Multiple types coexist simultaneously -- each gets its own independent `AsyncLocal` slot and is included in snapshots automatically:

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<UserContext>();
    ctx.Add<TenantContext>();
    ctx.Add<CorrelationContext>();
});
```

Each context type is accessed independently:

```csharp
var user = _accessor.GetContext<UserContext>();
var tenant = _accessor.GetContext<TenantContext>();
var correlation = _accessor.GetContext<CorrelationContext>();
```

## Usage patterns

### Pattern 1 -- Normal flow (no extra work)

When context is set in the current async flow, it is available everywhere via `IContextAccessor`:

```csharp
public async Task HandleAsync()
{
    var user = _accessor.GetContext<UserContext>();
    // safe across awaits -- AsyncLocal flows with ExecutionContext
    await DoWorkAsync();
}
```

### Pattern 2 -- Background work or `Task.Run`

When you offload work to `Task.Run` or a background thread, use `BeginScope()` on a snapshot to ensure context is available:

```csharp
var snapshot = _accessor.CreateSnapshot();

_ = Task.Run(async () =>
{
    using (snapshot.BeginScope())
    {
        // AsyncLocal is populated with snapshot values
        var user = _accessor.GetContext<UserContext>();
        await ProcessAsync(user);
    }
    // context is automatically cleaned up
});
```

### Pattern 3 -- Batch processing (parallel, isolated contexts)

Create snapshots directly from data -- no `AsyncLocal` mutation, fully parallel-safe:

```csharp
var workItems = messages
    .Select(m => (m, snapshot: _accessor.CreateSnapshot(
        new UserContext(m.UserId))))
    .ToList();

await Parallel.ForEachAsync(workItems, async (item, ct) =>
{
    using (item.snapshot.BeginScope())
    {
        await handler.HandleAsync(item.m);
        // each item has fully isolated context
    }
});
```

### Pattern 4 -- Nested scopes

Scopes are nestable. Each scope saves and restores only the context keys it touches:

```csharp
_writer.SetContext(new UserContext("root"));

using (snapshotA.BeginScope())
{
    // UserContext = A
    using (snapshotB.BeginScope())
    {
        // UserContext = B
    }
    // UserContext = A (restored)
}
// UserContext = "root" (restored)
```

### Pattern 5 -- Scoped snapshot via DI

The DI-registered `IContextSnapshot` is scoped -- it captures the ambient state at the time the scope is created. Inject it for immutable, request-scoped access:

```csharp
public class OrderService
{
    private readonly IContextSnapshot _snapshot;

    public OrderService(IContextSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public void Process()
    {
        var user = _snapshot.GetContext<UserContext>();
        // snapshot is immutable -- safe across async boundaries
    }
}
```

### Pattern 6 -- Required context (throws when missing)

```csharp
// Throws InvalidOperationException with a descriptive message if context is not set
var user = _accessor.GetRequiredContext<UserContext>();
var tenant = _snapshot.GetRequiredContext<TenantContext>();
```

## Domain-scoped context

Domains let different parts of your application maintain **isolated context values** for the same type. Think of them as **different accounts at the same bank** -- each domain has its own independent balance.

### Register domains

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<UserContext>();                                        // default (domainless)
    ctx.AddDomain("web-api", d => d.Add<UserContext>());          // web-api domain
    ctx.AddDomain("grpc", d => d.Add<UserContext>());             // grpc domain
});
```

### Read and write by domain

```csharp
_writer.SetContext(new UserContext("default-user"));
_writer.SetContext("web-api", new UserContext("web-user"));
_writer.SetContext("grpc", new UserContext("grpc-user"));

_accessor.GetContext<UserContext>();            // "default-user"
_accessor.GetContext<UserContext>("web-api");   // "web-user"
_accessor.GetContext<UserContext>("grpc");      // "grpc-user"
```

### Default domain selector

If your application should route parameterless calls to a specific domain, configure a `DefaultDomainSelector`:

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.AddDomain("web-api", d => d.Add<UserContext>());
    ctx.AddDomainPolicy(p => p.DefaultDomainSelector = sp =>
    {
        // Resolve the default domain at runtime from IServiceProvider
        return sp.GetRequiredService<ITenantResolver>().CurrentDomain;
    });
});
```

With this configured, `GetContext<UserContext>()` (no domain argument) delegates to the domain returned by the selector.

### Domain snapshots

Snapshots capture values across all domains and the default slot:

```csharp
// Capture everything
var snapshot = _accessor.CreateSnapshot();
snapshot.GetContext<UserContext>();            // default value
snapshot.GetContext<UserContext>("web-api");   // web-api value

// Create a snapshot for a specific domain
var webSnapshot = _accessor.CreateSnapshot("web-api", new UserContext("scoped"));
```

## How `AsyncLocal` works -- what you need to know

ContextR stores state using `System.Threading.AsyncLocal<T>`, the same mechanism used by `IHttpContextAccessor` in ASP.NET Core and `Activity.Current` in diagnostics.

| Scenario | Context flows? | Action needed |
|---|---|---|
| `await` calls | Yes | None |
| `Task.Run(...)` | Copy-on-write | Use snapshot |
| `ThreadPool.QueueUserWorkItem` | No | Use snapshot |
| `new Thread(...)` | No | Use snapshot |
| Fire-and-forget (`_ = DoAsync()`) | Fragile | Use snapshot |

### The snapshot solves this

The snapshot captures all context values at a point in time into an immutable object. `BeginScope()` writes those values into the current `AsyncLocal`, and `Dispose()` restores the previous state. This gives you:

- Reliable propagation across any boundary
- No interference with the parent flow (scoped to the current `ExecutionContext`)
- Automatic cleanup via `using`

## Recommendations

### Prefer snapshots in service code

Use `IContextAccessor` / `IContextWriter` only for infrastructure. For service-level code, prefer `IContextSnapshot` (injected via DI) or manually created snapshots:

```csharp
// Reading -- use snapshot
var user = _snapshot.GetContext<UserContext>();

// Background work -- activate snapshot
_ = Task.Run(async () =>
{
    using (snapshot.BeginScope())
    {
        await DoWorkAsync();
    }
});
```

### Always use `using` with `BeginScope()`

```csharp
// Correct
using (snapshot.BeginScope())
{
    await DoWorkAsync();
}

// Incorrect -- context leaks into subsequent work
snapshot.BeginScope();
await DoWorkAsync();
```

### Capture snapshots early

Create the snapshot while context is still alive:

```csharp
// Correct -- captured while context is alive
_writer.SetContext(new UserContext("alice"));
var snapshot = _accessor.CreateSnapshot();
queue.Enqueue(() => UseSnapshot(snapshot));

// Risky -- context may be gone when job runs
queue.Enqueue(() =>
{
    var snapshot = _accessor.CreateSnapshot(); // may capture empty context
});
```

### Do not mutate context objects from snapshots

Snapshots hold references, not deep copies. Treat context objects as read-only.

## Thread safety

The underlying storage uses `ConcurrentDictionary<ContextKey, AsyncLocal<ContextHolder>>`. All read and write operations are thread-safe without explicit locking. Snapshots are immutable after creation. Concurrent `BeginScope()` calls on different threads operate on independent `AsyncLocal` slots and do not interfere with each other.

## Builder validation

ContextR validates configuration at startup:

- If domain registrations exist but no default (domainless) `Add<T>()` is configured and no `DefaultDomainSelector` is provided, `AddContextR` throws `InvalidOperationException`. This prevents runtime errors when `GetContext<T>()` is called without a domain argument.

## API reference

### Extension methods on `IContextAccessor`

| Method | Description |
|---|---|
| `CreateSnapshot()` | Captures all current ambient values (across all domains) into an immutable snapshot. |
| `CreateSnapshot<T>(T context)` | Creates a snapshot containing only the provided value in the default domain. No `AsyncLocal` mutation. |
| `CreateSnapshot<T>(string domain, T context)` | Creates a snapshot containing only the provided value for the specified domain. No `AsyncLocal` mutation. |
| `GetRequiredContext<T>()` | Returns the context value or throws `InvalidOperationException` if missing. |
| `GetRequiredContext<T>(string domain)` | Returns the domain-specific context value or throws. |

### Extension methods on `IContextSnapshot`

| Method | Description |
|---|---|
| `GetRequiredContext<T>()` | Returns the captured value or throws `InvalidOperationException`. |
| `GetRequiredContext<T>(string domain)` | Returns the domain-specific captured value or throws. |

## What ContextR is NOT

- **Not tied to HTTP, gRPC, or any transport.** It is a pure context-propagation library. Integration with specific transports is left to the consumer.
- **Not a replacement for `Activity` / distributed tracing.** `Activity` is for trace IDs and spans. ContextR is for application-level context values.
- **Not a DI container.** It stores ambient values in `AsyncLocal`, not in the service provider.
