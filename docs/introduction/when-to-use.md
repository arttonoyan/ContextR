# When To Use ContextR

Use ContextR when your system needs typed operational context to move consistently across ingress, internal service calls, and asynchronous execution paths.

## Good Fit

ContextR is a strong fit when:

- services communicate over both HTTP and gRPC
- request context must continue into background jobs
- teams need policy-driven handling for missing or malformed values
- infrastructure code should handle context propagation, not business code

## Not A Fit

ContextR is not intended for:

- authentication token validation or authorization logic
- large payload transport that should use message bodies or storage references
- simple applications where explicit method parameters are sufficient

## Related Pages

- [Why ContextR Was Born](../WhyContextR.md)
- [Getting Started](../GettingStarted.md)
- [Q&A / FAQ](../FAQ.md)
