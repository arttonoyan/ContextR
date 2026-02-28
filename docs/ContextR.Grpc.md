# ContextR.Transport.Grpc

gRPC context propagation for ContextR. This package provides interceptors and helper extensions to propagate ambient context through gRPC metadata on the client side and extract it on the server side.

## When to use this package

Use `ContextR.Transport.Grpc` when your services communicate over gRPC and you want typed context values (correlation IDs, tenant identifiers, feature flags) to flow automatically between caller and callee.

## Install

```
dotnet add package ContextR.Transport.Grpc
```

Dependencies: `ContextR` (core), `ContextR.Propagation` (for `IContextPropagator<T>`), `Grpc.Core.Api`, `Grpc.Net.ClientFactory`.

## Client-side propagation

### Global propagation (all gRPC clients)

Apply ContextR propagation to all gRPC clients registered via `AddGrpcClient`:

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<CorrelationContext>(reg => reg
        .MapProperty(c => c.TraceId, "x-trace-id")
        .UseGlobalGrpcPropagation());
});
```

`UseGlobalGrpcPropagation()` captures the registration domain (if any) and registers interceptor options through `GrpcClientFactoryOptions`.

### Per-client propagation

Apply propagation only to a specific gRPC client:

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<CorrelationContext>(reg => reg
        .MapProperty(c => c.TraceId, "x-trace-id"));
});

builder.Services.AddGrpcClient<MyGrpcClient>(options =>
{
    options.Address = new Uri("https://localhost:5001");
})
.AddContextRGrpcPropagation<CorrelationContext>();
```

`AddContextRGrpcPropagation<T>()` is an extension on `IHttpClientBuilder`.

## Server-side extraction

`ContextInterceptor<TContext>` extracts gRPC metadata into a typed context object through `IContextPropagator<TContext>` and writes it to ambient storage via `IContextWriter`.

Register it as a gRPC server interceptor:

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<CorrelationContext>(reg => reg
        .MapProperty(c => c.TraceId, "x-trace-id"));
});

builder.Services.AddTransient<ContextInterceptor<CorrelationContext>>();
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<ContextInterceptor<CorrelationContext>>();
});
```

After that, gRPC handlers can read ambient values with `IContextAccessor` / `IContextSnapshot`.

## Domain-aware behavior

When configured in a domain, interceptors are domain-aware:

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<CorrelationContext>()
       .AddDomain("grpc", d => d.Add<CorrelationContext>(reg => reg
           .MapProperty(c => c.TraceId, "x-trace-id")
           .UseGlobalGrpcPropagation()));
});
```

In this case, client propagation reads from `_accessor.GetContext<TContext>("grpc")` rather than the default slot.

## Metadata key handling

gRPC metadata keys are treated as lowercase in transport helpers. ContextR gRPC adapters normalize keys to lowercase before read/write to keep behavior consistent with gRPC conventions.

## File map

| File | Role |
|---|---|
| `ContextPropagationInterceptor.cs` | Client interceptor that injects context into outgoing metadata |
| `ContextInterceptor.cs` | Server interceptor that extracts metadata and writes ambient context |
| `GrpcMetadataContextPropagatorExtensions.cs` | Metadata adapter helpers for `IContextPropagator<T>` |
| `Extensions/ContextRGrpcRegistrationExtensions.cs` | `UseGlobalGrpcPropagation()` on `IContextRegistrationBuilder<T>` |
| `Extensions/ContextRGrpcClientBuilderExtensions.cs` | Per-client gRPC extensions on `IHttpClientBuilder` / `GrpcClientFactoryOptions` |
