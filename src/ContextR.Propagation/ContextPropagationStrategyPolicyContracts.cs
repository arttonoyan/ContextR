namespace ContextR.Propagation;

/// <summary>
/// Runtime context provided to strategy policy evaluation.
/// </summary>
public sealed class ContextPropagationStrategyPolicyContext
{
    /// <summary>
    /// Propagation context type.
    /// </summary>
    public required Type ContextType { get; init; }

    /// <summary>
    /// Mapped transport key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Mapped property type.
    /// </summary>
    public required Type PropertyType { get; init; }

    /// <summary>
    /// Propagation direction.
    /// </summary>
    public required PropagationDirection Direction { get; init; }

    /// <summary>
    /// Active domain during propagation.
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// Optional payload size in UTF-8 bytes if known.
    /// </summary>
    public int? PayloadSizeBytes { get; init; }
}

/// <summary>
/// Chooses effective oversize strategy for a mapped property at runtime.
/// </summary>
public interface IContextPropagationStrategyPolicy<TContext>
    where TContext : class
{
    /// <summary>
    /// Returns selected oversize behavior for the current property operation.
    /// </summary>
    ContextOversizeBehavior Select(ContextPropagationStrategyPolicyContext context);
}
