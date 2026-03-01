namespace ContextR.Resolution;

/// <summary>
/// Orchestrates context resolution from resolver + propagated sources and optionally writes the final value.
/// </summary>
/// <typeparam name="TContext">The context type being resolved.</typeparam>
public interface IContextResolutionOrchestrator<TContext>
    where TContext : class
{
    /// <summary>
    /// Resolves a final context value for the current operation.
    /// </summary>
    /// <param name="context">Resolution operation metadata.</param>
    /// <param name="propagatedContext">Optional propagated value from upstream extraction.</param>
    /// <returns>Final resolution result.</returns>
    ContextResolutionResult<TContext> Resolve(ContextResolutionContext context, TContext? propagatedContext = null);

    /// <summary>
    /// Resolves and writes the final context into ambient storage.
    /// </summary>
    /// <param name="context">Resolution operation metadata.</param>
    /// <param name="propagatedContext">Optional propagated value from upstream extraction.</param>
    /// <returns>Final resolution result.</returns>
    ContextResolutionResult<TContext> ResolveAndWrite(ContextResolutionContext context, TContext? propagatedContext = null);
}
