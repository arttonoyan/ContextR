namespace ContextR.Resolution;

/// <summary>
/// Final outcome of context resolution.
/// </summary>
/// <typeparam name="TContext">The context type being resolved.</typeparam>
public sealed class ContextResolutionResult<TContext>
    where TContext : class
{
    /// <summary>
    /// The resulting context value, if any.
    /// </summary>
    public TContext? Context { get; init; }

    /// <summary>
    /// Source used to produce <see cref="Context"/>.
    /// </summary>
    public required ContextResolutionSource Source { get; init; }

    /// <summary>
    /// Convenience factory for a missing-resolution result.
    /// </summary>
    public static ContextResolutionResult<TContext> None()
    {
        return new ContextResolutionResult<TContext>
        {
            Context = null,
            Source = ContextResolutionSource.None
        };
    }
}
