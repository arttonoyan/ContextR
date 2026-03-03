# Propagation Chunking

`ContextR.Propagation.Chunking` provides chunk split/reassembly for oversize metadata payloads.

[Back to Packages Overview](index.md){ .md-button }

## Use This Package When

- payloads can exceed practical header/metadata limits
- `ChunkProperty` is selected as oversize behavior
- inline JSON payloads need deterministic fragmentation and recovery

## Install

```bash
dotnet add package ContextR.Propagation.Chunking
```

## Depends On

- `ContextR.Propagation`

## See Also

- [Chunking Details](../ContextR.Propagation.Chunking.md)
- [Packages Overview](index.md)
