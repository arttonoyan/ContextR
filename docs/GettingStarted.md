# Getting Started

This guide gets ContextR running in minutes for a typical ASP.NET Core service with outgoing `HttpClient` calls.

## 1) Pick packages

Minimum for ambient context only:

```bash
dotnet add package ContextR
```

For propagation with mapping:

```bash
dotnet add package ContextR
dotnet add package ContextR.Propagation
dotnet add package ContextR.Propagation.Mapping
```

For web + outgoing HTTP propagation:

```bash
dotnet add package ContextR.Hosting.AspNetCore
dotnet add package ContextR.Transport.Http
```

Optional strategy packages:

- `ContextR.Propagation.InlineJson` for non-primitive payload serialization
- `ContextR.Propagation.Chunking` for oversize chunk split/reassembly
- `ContextR.Propagation.Token` for token/reference contracts

## 2) Define a context contract

```csharp
public sealed class UserContext
{
    public string? TraceId { get; set; }
    public string? TenantId { get; set; }
    public string? UserId { get; set; }
}
```

## 3) Register ContextR

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
```

## 4) Build and run app

```csharp
var app = builder.Build();

app.MapControllers();

app.Run();
```

No explicit `app.UseContextR()` call is required.  
`UseAspNetCore()` registers extraction middleware automatically through `IStartupFilter`.

## 5) Use context in app code

Prefer snapshots in application services:

```csharp
public sealed class OrderService
{
    private readonly IContextSnapshot _snapshot;

    public OrderService(IContextSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public string? GetTenant() => _snapshot.GetContext<UserContext>()?.TenantId;
}
```

## 6) Add complex property support (optional)

```csharp
ctx.Add<ExtendedContext>(reg => reg
    .UseInlineJsonPayloads<ExtendedContext>(o =>
    {
        o.MaxPayloadBytes = 2048;
        o.OversizeBehavior = ContextOversizeBehavior.SkipProperty;
    })
    .MapProperty(c => c.Tags, "X-Tags"));
```

## 7) Validate behavior with tests

Recommended first tests:

- incoming header extraction fills context
- outgoing `HttpClient` includes mapped headers
- required property failure behavior matches policy
- oversize behavior is deterministic

For full examples, see [samples](../samples/README.md).
