# ContextR.OpenFeature

`ContextR.OpenFeature` connects ambient ContextR values to OpenFeature `EvaluationContext`.

It is built for teams using the OpenFeature .NET SDK: <https://github.com/open-feature/dotnet-sdk/>.

## Why this integration is useful

`ContextR.OpenFeature` helps teams that already standardized request/user/tenant/correlation context with ContextR and now need reliable feature-flag targeting in OpenFeature.

Without this integration, most applications repeat the same `AddContext` setup logic in each service. That repetition increases drift risk: one service maps keys differently, another forgets targeting key, another misses domain context. Over time this causes inconsistent flag behavior across environments and services.

With `ContextR.OpenFeature`, teams can define mapping once in a consistent fluent API and reuse ambient ContextR state directly. This improves:

- **Consistency**: same targeting key, kind, and attributes across services.
- **Maintainability**: fewer duplicated context builders to review and update.
- **Safety**: built-in collision and filtering policies reduce accidental misconfiguration.
- **Domain readiness**: supports domain-aware mapping for multi-provider and multi-tenant setups.
- **Adoption speed**: easier migration from custom per-app context wiring to a shared pattern.

## Who this helps most

- Multi-tenant SaaS platforms where tenant/user context drives rollouts.
- Microservice systems that need the same flag targeting model in many services.
- Teams adopting OpenFeature incrementally and wanting minimal boilerplate in each app.
- Organizations enforcing platform standards for observability and context propagation.

## Install

```bash
dotnet add package ContextR.OpenFeature
```

## API variants

### 1) Zero-config

Use this when you only want to enable the bridge and add mappings later.

```csharp
builder.Services.AddOpenFeature(feature => feature.UseContextR());
```

### 2) Single-context convenience (`UseContextR<TContext>`)

Use this for focused setups where one primary context drives most targeting behavior.

```csharp
builder.Services.AddOpenFeature(feature =>
{
    feature.UseContextR<UserContext>(options =>
    {
        options.SetTargetingKey(x => x.UserId);
        options.SetContextKind("user");
        options.Map(map => map
            .ByConvention("user.")
            .Ignore(x => x.InternalToken));
    });
});
```

### 3) Composable multi-context (`UseContextR`)

Use this when `EvaluationContext` is composed from multiple ContextR types.

```csharp
builder.Services.AddOpenFeature(feature =>
{
    feature.UseContextR(options =>
    {
        options.SetTargetingKey<UserContext>(x => x.UserId);
        options.SetContextKind("user");

        options.Map<UserContext>(map => map.MapProperty(x => x.Email, "user.email"));
        options.Map<TenantContext>(map => map.MapProperty(x => x.TenantId, "tenant.id"));
        options.Map<CorrelationContext>(map => map.MapProperty(x => x.TraceId, "trace.id"));
    });
});
```

### 4) Domain-aware mappings (ContextR domains)

Use ContextR domain-aware mapping when ambient values are stored per domain.

```csharp
builder.Services
    .AddContextR(ctx =>
    {
        ctx.AddDomain("tenant-a", d => d.Add<UserContext>());
        ctx.AddDomainPolicy(_ => "tenant-a");
    })
    .AddOpenFeature(feature =>
    {
        feature.UseContextR(options =>
        {
            options.Map<UserContext>("tenant-a", map =>
                map.MapProperty(x => x.UserId, "user.id"));
        });
    });
```

## Targeting key and kind

### Typed targeting key (recommended)

```csharp
feature.UseContextR<UserContext>(options =>
{
    options.SetTargetingKey(x => x.UserId);
    options.SetContextKind("user");
});
```

### Advanced targeting key fallback

Use when key selection needs custom runtime logic.

```csharp
feature.UseContextR(options =>
{
    options.SetTargetingKey(sp =>
    {
        var accessor = sp.GetRequiredService<IContextAccessor>();
        return accessor.GetContext<UserContext>()?.UserId;
    });
});
```

## Mapping customization

### Explicit property mapping

```csharp
feature.UseContextR(options =>
{
    options.Map<TenantContext>(map =>
        map.MapProperty(x => x.TenantId, "tenant.id"));
});
```

### Convention mapping with ignore list

```csharp
feature.UseContextR<UserContext>(options =>
{
    options.Map(map => map
        .ByConvention("user.")
        .Ignore(x => x.PasswordHash)
        .Ignore(x => x.InternalToken));
});
```

### Collision and filtering policies

```csharp
feature.UseContextR(options =>
{
    options.CollisionBehavior = ContextROpenFeatureCollisionBehavior.Throw;
    options.UnsupportedValueBehavior = ContextROpenFeatureUnsupportedValueBehavior.Ignore;
    options.AllowKeys("kind", "tenant.id", "user.id", "user.email");
    options.BlockKeys("user.internal");
});
```

## Complete example

```csharp
builder.Services
    .AddContextR(ctx =>
    {
        ctx.Add<UserContext>();
        ctx.Add<TenantContext>();
        ctx.Add<CorrelationContext>();
    })
    .AddOpenFeature(feature =>
    {
        feature
            .UseContextR(options =>
            {
                options.SetTargetingKey<UserContext>(x => x.UserId);
                options.SetContextKind("user");
                options.Map<UserContext>(map => map
                    .ByConvention("user.")
                    .Ignore(x => x.InternalToken));
                options.Map<TenantContext>(map => map.MapProperty(x => x.TenantId, "tenant.id"));
                options.Map<CorrelationContext>(map => map.MapProperty(x => x.TraceId, "trace.id"));
            })
            .AddHook<LoggingHook>();
    });
```
