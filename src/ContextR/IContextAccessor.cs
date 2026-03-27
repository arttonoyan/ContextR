namespace ContextR;

/// <summary>
/// Provides access to live ambient context values for the current asynchronous flow.
/// </summary>
public interface IContextAccessor
{
    /// <summary>
    /// Gets the ambient context value of the specified <paramref name="contextType"/>, if present.
    /// </summary>
    /// <param name="contextType">The context type.</param>
    /// <returns>The current context value, or <see langword="null"/> when not set.</returns>
    object? GetContext(Type contextType);

    /// <summary>
    /// Gets the ambient context value of the specified <paramref name="contextType"/>
    /// for the specified domain, if present.
    /// </summary>
    /// <param name="domain">The domain to read from.</param>
    /// <param name="contextType">The context type.</param>
    /// <returns>The current context value, or <see langword="null"/> when not set.</returns>
    object? GetContext(string domain, Type contextType);

    /// <summary>
    /// Captures a snapshot of all current ambient context values (across all domains).
    /// </summary>
    /// <returns>An immutable snapshot of current ambient context values.</returns>
    IContextSnapshot CaptureSnapshot();

    /// <summary>
    /// Creates a snapshot containing only the provided context value in the default domain,
    /// without touching ambient state.
    /// </summary>
    /// <param name="contextType">The context type.</param>
    /// <param name="context">The context value to include in the snapshot.</param>
    /// <returns>An immutable snapshot containing only <paramref name="context"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    IContextSnapshot CreateSnapshot(Type contextType, object context);

    /// <summary>
    /// Creates a snapshot containing only the provided context value for the specified domain,
    /// without touching ambient state.
    /// </summary>
    /// <param name="domain">The domain to associate the context value with.</param>
    /// <param name="contextType">The context type.</param>
    /// <param name="context">The context value to include in the snapshot.</param>
    /// <returns>An immutable snapshot containing only <paramref name="context"/> for <paramref name="domain"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    IContextSnapshot CreateSnapshot(string domain, Type contextType, object context);
}
