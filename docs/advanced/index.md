# Advanced

This section covers operational patterns and edge-case guidance for production environments.

## Topics

- [Usage Cookbook](../UsageCookbook.md)
- [Q&A / FAQ](../FAQ.md)
- [Context Resolution](../ContextR.Resolution.md)

## Recommended Usage

- Keep transport integrations thin and deterministic.
- Keep business code snapshot-first.
- Document required context fields and failure policy per boundary.
- Add explicit tests for scope, parallelism, and oversize behavior.
