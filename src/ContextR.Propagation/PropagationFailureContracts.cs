namespace ContextR.Propagation;

/// <summary>
/// Direction of propagation operation where a failure occurred.
/// </summary>
public enum PropagationDirection
{
    /// <summary>
    /// Context was being injected into a carrier.
    /// </summary>
    Inject = 0,

    /// <summary>
    /// Context was being extracted from a carrier.
    /// </summary>
    Extract = 1
}

/// <summary>
/// Categorized reason for propagation failure.
/// </summary>
public enum PropagationFailureReason
{
    /// <summary>
    /// A required mapped property was missing.
    /// </summary>
    MissingRequired = 0,

    /// <summary>
    /// A mapped property could not be parsed.
    /// </summary>
    ParseFailed = 1,

    /// <summary>
    /// Mapped payload exceeded configured size.
    /// </summary>
    Oversize = 2,

    /// <summary>
    /// Token fallback was requested but no token strategy was configured.
    /// </summary>
    TokenFallbackUnavailable = 3,

    /// <summary>
    /// Unexpected propagation failure.
    /// </summary>
    Unexpected = 4
}

/// <summary>
/// Action returned by propagation failure handler.
/// </summary>
public enum PropagationFailureAction
{
    /// <summary>
    /// Re-throw/propagate the failure.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Skip only the current mapped property and continue.
    /// </summary>
    SkipProperty = 1,

    /// <summary>
    /// Skip the entire context propagation operation.
    /// </summary>
    SkipContext = 2
}

/// <summary>
/// Failure details passed to a propagation failure handler.
/// </summary>
public sealed class PropagationFailureContext
{
    /// <summary>
    /// Context type being propagated.
    /// </summary>
    public required Type ContextType { get; init; }

    /// <summary>
    /// Transport key associated with the failure.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Operation direction where failure occurred.
    /// </summary>
    public required PropagationDirection Direction { get; init; }

    /// <summary>
    /// Categorized failure reason.
    /// </summary>
    public required PropagationFailureReason Reason { get; init; }

    /// <summary>
    /// Optional domain associated with current registration.
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// Optional raw value involved in failure.
    /// </summary>
    public string? RawValue { get; init; }

    /// <summary>
    /// Optional underlying exception.
    /// </summary>
    public Exception? Exception { get; init; }
}

/// <summary>
/// Handles propagation failures for a context type.
/// </summary>
/// <typeparam name="TContext">Context type.</typeparam>
public interface IContextPropagationFailureHandler<TContext>
    where TContext : class
{
    /// <summary>
    /// Handles a propagation failure and returns desired action.
    /// </summary>
    PropagationFailureAction Handle(PropagationFailureContext failure);
}
