# Coverage Exclusions

This document records the intentionally small set of types excluded from coverage metrics.

## Policy

- Exclude only data-container types with little or no branching behavior.
- Do not exclude behavior-heavy types (registries, extensions, propagators, middleware, policies, orchestrators).
- Keep exclusions explicit and centralized in `Directory.Build.props`.
- Prefer adding tests over adding new exclusions.

## Current Exclusions

These exclusions mirror the `Exclude` filter in `Directory.Build.props`:

- `ContextR.Resolution.ContextResolutionContext`
  - Input DTO passed to resolvers and orchestrator.
- `ContextR.Resolution.ContextResolutionResult<T>`
  - Output DTO returned from resolution policy/orchestration.
- `ContextR.Resolution.ContextResolutionPolicyContext<T>`
  - Aggregated input DTO consumed by resolution policies.

## Current Status

- `PropagationExecutionContext` and `PropagationFailureContext` are included in coverage.
- Core behavior classes remain included and are expected to be validated by tests.
- Latest merged report baseline:
  - Line coverage: `98.7%`
  - Branch coverage: `95%` (439/462)
  - Method coverage: `99.2%`
