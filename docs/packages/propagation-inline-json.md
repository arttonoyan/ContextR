# Propagation Inline JSON

`ContextR.Propagation.InlineJson` adds JSON serialization for complex mapped properties and deterministic payload-size controls.

[Back to Packages Overview](index.md){ .md-button }

## Use This Package When

- mapped properties include lists, arrays, or custom classes
- transport metadata must carry compact serialized payloads
- oversize behavior must be explicitly configured

## Install

```bash
dotnet add package ContextR.Propagation.InlineJson
```

## Depends On

- `ContextR`
- `ContextR.Propagation`

## See Also

- [Inline JSON Details](../ContextR.Propagation.InlineJson.md)
