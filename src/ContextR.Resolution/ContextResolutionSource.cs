namespace ContextR.Resolution;

/// <summary>
/// Indicates which source produced the final resolved context.
/// </summary>
public enum ContextResolutionSource
{
    /// <summary>
    /// No context could be resolved.
    /// </summary>
    None = 0,

    /// <summary>
    /// Resolved context from registered resolver.
    /// </summary>
    Resolver = 1,

    /// <summary>
    /// Resolved context from propagated upstream value.
    /// </summary>
    Propagated = 2,

    /// <summary>
    /// Resolved by a custom policy merge/decision.
    /// </summary>
    Policy = 3
}
