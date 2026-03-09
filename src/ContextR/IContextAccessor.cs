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

    /// <summary>
    /// Gets the ambient context value of type <typeparamref name="TContext"/> for the specified domain, if present.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="domain">The domain to read from.</param>
    /// <returns>The current context value, or <see langword="null"/> when not set.</returns>
    TContext? GetContext<TContext>(string domain) where TContext : class;

    /// <summary>
    /// Captures a snapshot of all current ambient context values (across all domains).
    /// </summary>
    /// <returns>An immutable snapshot of current ambient context values.</returns>
    IContextSnapshot CreateSnapshot();

    /// <summary>
    /// Creates a snapshot containing only the provided context value in the default domain,
    /// without touching ambient state.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="context">The context value to include in the snapshot.</param>
    /// <returns>An immutable snapshot containing only <paramref name="context"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    IContextSnapshot CreateSnapshot<TContext>(TContext context) where TContext : class;

    /// <summary>
    /// Creates a snapshot containing only the provided context value for the specified domain,
    /// without touching ambient state.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="domain">The domain to associate the context value with.</param>
    /// <param name="context">The context value to include in the snapshot.</param>
    /// <returns>An immutable snapshot containing only <paramref name="context"/> for <paramref name="domain"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    IContextSnapshot CreateSnapshot<TContext>(string domain, TContext context) where TContext : class;
}
