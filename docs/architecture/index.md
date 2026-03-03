# Architecture

This section explains how ContextR is implemented and why its design choices are intentionally conservative for distributed .NET systems.

## Topics

- [Architecture and Design Decisions](../ARCHITECTURE.md)

## What To Focus On

- `AsyncLocal` storage model and context keying by domain and type
- snapshot capture and `BeginScope()` restoration behavior
- singleton accessor/writer design and DI scope boundaries
- propagator abstraction and transport integration points
