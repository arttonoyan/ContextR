# Hosting ASP.NET Core

`ContextR.Hosting.AspNetCore` enables ingress extraction from HTTP request headers into ambient context.

[Back to Packages Overview](index.md){ .md-button }

## Use This Package When

- ASP.NET Core services receive context at ingress
- extracted context must be available to request pipeline and downstream integrations
- ingress enforcement policy is required

## Install

```bash
dotnet add package ContextR.Hosting.AspNetCore
```

## Depends On

- `ContextR`
- `ContextR.Propagation`
- `ContextR.Transport.Http`

## See Also

- [ASP.NET Core Ingress](../ContextR.AspNetCore.md)
