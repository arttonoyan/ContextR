# ContextR.OpenFeature

OpenFeature.Hosting integration for ContextR.

Use this package to project ambient ContextR values into OpenFeature `EvaluationContext`
without manually repeating `AddContext` wiring in each application.

## Install

```bash
dotnet add package ContextR.OpenFeature
```

## Quick start

```csharp
builder.Services
    .AddContextR(ctx => ctx.Add<UserContext>())
    .AddOpenFeature(feature => feature.UseContextR<UserContext>(o =>
    {
        o.SetTargetingKey(x => x.UserId);
        o.SetContextKind("user");
        o.Map(m => m.ByConvention("user."));
    }));
```

## Zero-config behavior

If you call `UseContextR()` without options:

```csharp
builder.Services.AddOpenFeature(feature => feature.UseContextR());
```

the package still wires OpenFeature `AddContext`, but it does not add any mappings by default.

- `EvaluationContext` is generated per DI resolution from the current ambient ContextR values.
- Only configured mappings contribute attributes (`MapProperty`, `ByConvention`, etc.).
- `targetingKey` is not auto-inferred unless you set it via `SetTargetingKey(...)`.
- `kind` is not auto-inferred unless you set it via `SetContextKind(...)`.

So in strict zero-config mode, `EvaluationContext` is typically empty until you configure mapping and/or key/kind selectors.

See `docs/ContextR.OpenFeature.md` for all API variants and advanced examples.
