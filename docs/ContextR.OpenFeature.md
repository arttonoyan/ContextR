# ContextR.OpenFeature

`ContextR.OpenFeature` connects ambient ContextR values to OpenFeature `EvaluationContext`.

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
