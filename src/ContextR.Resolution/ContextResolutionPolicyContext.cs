namespace ContextR.Resolution;

/// <summary>
/// Inputs provided to a context resolution policy.
/// </summary>
/// <typeparam name="TContext">The context type being resolved.</typeparam>
public sealed class ContextResolutionPolicyContext<TContext>
    where TContext : class
{
    /// <summary>
    /// Runtime resolution metadata.
    /// </summary>
    public required ContextResolutionContext ResolutionContext { get; init; }

    /// <summary>
    /// Context value resolved from registered resolver(s), if any.
    /// </summary>
    public TContext? ResolvedContext { get; init; }

    /// <summary>
    /// Context value extracted from propagation input, if any.
    /// </summary>
    public TContext? PropagatedContext { get; init; }
}
