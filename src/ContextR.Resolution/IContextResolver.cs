namespace ContextR.Resolution;

/// <summary>
/// Resolves a context value from runtime sources at ingress (JWT, host, path, claims, etc.).
/// </summary>
/// <typeparam name="TContext">The context type to resolve.</typeparam>
public interface IContextResolver<TContext>
    where TContext : class
{
    /// <summary>
    /// Attempts to resolve the context value for the current operation.
    /// </summary>
    /// <param name="context">Resolution operation metadata.</param>
    /// <returns>Resolved context value, or <see langword="null"/> when unavailable.</returns>
    TContext? Resolve(ContextResolutionContext context);
}
