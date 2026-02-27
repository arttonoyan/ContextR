namespace ContextR;

/// <summary>
/// Provides access to live ambient context values for the current asynchronous flow.
/// </summary>
public interface IContextAccessor
{
    /// <summary>
    /// Gets the ambient context value of type <typeparamref name="TContext"/>, if present.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <returns>The current context value, or <see langword="null"/> when not set.</returns>
    TContext? GetContext<TContext>() where TContext : class;
}
