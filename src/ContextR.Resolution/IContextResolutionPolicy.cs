namespace ContextR.Resolution;

/// <summary>
/// Decides the final context value when resolver and propagated sources are available.
/// </summary>
/// <typeparam name="TContext">The context type being resolved.</typeparam>
public interface IContextResolutionPolicy<TContext>
    where TContext : class
{
    /// <summary>
    /// Resolves the final context value from available inputs.
    /// </summary>
    /// <param name="context">Policy input values and metadata.</param>
    /// <returns>Final resolution result.</returns>
    ContextResolutionResult<TContext> Resolve(ContextResolutionPolicyContext<TContext> context);
}
