# Transport HTTP

`ContextR.Transport.Http` provides `HttpClient` propagation handlers and registration helpers.

[Back to Packages Overview](index.md){ .md-button }

## Use This Package When

- outgoing HTTP calls must carry context headers
- propagation should be global or selective per client
- handler-scope concerns require deterministic ambient reads at send time

## Install

```bash
dotnet add package ContextR.Transport.Http
```

## Depends On

- `ContextR`
- `ContextR.Propagation`

## See Also

- [HTTP Client Egress](../ContextR.Http.md)
- [HTTP Handler Scopes](../HttpClientHandlerScopes.md)
