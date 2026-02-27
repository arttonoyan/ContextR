# ContextR
Lightweight execution context propagation and snapshot library for .NET.

## Core Mental Model

- Accessor = live ambient context for the current async flow.
- Snapshot = immutable captured state.
- BeginScope = activation boundary that applies a snapshot and restores previous values when disposed.

```csharp
services.AddContextR(builder =>
{
    builder.Add<UserContext>();
});
```

```csharp
var snapshot = accessor.Capture();

using (snapshot.BeginScope())
{
    var currentUser = accessor.Get<UserContext>();
    // execute work with snapshot active
}
```
