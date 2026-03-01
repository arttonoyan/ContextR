namespace ContextR.Resolution;

/// <summary>
/// Runtime metadata describing the current context resolution operation.
/// </summary>
public sealed class ContextResolutionContext
{
    /// <summary>
    /// Ingress trust boundary for this resolution.
    /// </summary>
    public required ContextIngressBoundary Boundary { get; init; }

    /// <summary>
    /// Optional ContextR domain for this operation.
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// Optional transport or pipeline source label (for diagnostics/policies).
    /// </summary>
    public string? Source { get; init; }
}
