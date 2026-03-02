# ContextR.Resolution

`ContextR.Resolution` provides ingress context resolution for ContextR.

It is separate from `Propagation`:

- **Resolution**: derive context from trusted runtime sources (JWT claims, host/path, custom sources).
- **Propagation**: move context across transport boundaries (HTTP/gRPC/events).

## Why this exists

In distributed systems, first-hop context creation and downstream context propagation are different responsibilities.  
Mixing them leads to boilerplate and inconsistent trust rules.

`Resolution` provides a unified place to decide final context values at ingress.

## Core contracts

- `IContextResolver<TContext>`: resolve context value from runtime source.
- `IContextResolutionPolicy<TContext>`: choose final value when both resolved and propagated values exist.
- `IContextResolutionOrchestrator<TContext>`: execute resolver + policy and optionally write ambient context.
- `ContextResolutionContext`: boundary/domain metadata.
- `ContextIngressBoundary`: `External` or `Internal`.
- `ContextResolutionResult<TContext>` and `ContextResolutionSource`.

## Default precedence model

Default policy is trust-boundary split:

- **External ingress**: resolver wins over propagated
- **Internal ingress**: propagated wins over resolver

Fallback behavior:

- if only one source exists, that source is used
- if no source exists, result is empty (`Source = None`)

## Registration API

Enable resolution from inside `AddContextR(...)`:

```csharp
builder.Services.AddContextR(ctx =>
{
    ctx.Add<UserContext>(reg => reg
        .AddResolution(r => r
            .UseResolver<JwtUserContextResolver>()));
});
```

`UseResolver(...)` and `UseResolutionPolicy(...)` also auto-enable resolution services.

That means these two patterns are both valid:

- direct resolver/policy registration and let ContextR auto-register resolution services (recommended)
- explicit package bootstrap with `AddContextRResolution()`, then resolver/policy registration (advanced/optional)

Resolver registration (recommended):

```csharp
ctx.Add<UserContext>(reg => reg
    .AddResolution(r => r
        .UseResolver<JwtUserContextResolver>()));
```

Or direct resolver registration without nested builder:

```csharp
ctx.Add<UserContext>(reg => reg
    .UseResolver<UserContext, JwtUserContextResolver>());
```

Or delegate-based resolver:

```csharp
ctx.Add<UserContext>(reg => reg
    .UseResolver<UserContext>(resolution => new UserContext("u1")));
```

Policy registration:

```csharp
ctx.Add<UserContext>(reg => reg
    .AddResolution(r => r
        .UseResolutionPolicy<CustomUserResolutionPolicy>()));
```

Or delegate-based policy:

```csharp
ctx.Add<UserContext>(reg => reg
    .UseResolutionPolicy<UserContext>(policyContext =>
        new ContextResolutionResult<UserContext>
        {
            Context = policyContext.ResolvedContext,
            Source = ContextResolutionSource.Policy
        }));
```

## Orchestrator usage

```csharp
var result = orchestrator.ResolveAndWrite(
    new ContextResolutionContext
    {
        Boundary = ContextIngressBoundary.External,
        Domain = "web-api"
    },
    propagatedContext);
```

`ResolveAndWrite` writes final context through `IContextWriter` when a value is selected.

## Real-world example

For a production-style gateway flow (JWT claims -> `UserContext` -> downstream propagation), see:

- [GatewayIngressResolution sample](../samples/GatewayIngressResolution/README.md)

## Package boundaries

`ContextR.Resolution` is optional and depends on `ContextR` core primitives (`IContextWriter`, domains, snapshots/accessor model).  
Use this package in gateway/ingress services that need first-hop context creation policies.
