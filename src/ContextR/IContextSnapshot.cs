namespace ContextR;

/// <summary>
/// Represents an immutable captured state of ambient contexts.
/// </summary>
public interface IContextSnapshot
{
    /// <summary>
    /// Gets a captured context value of the specified <paramref name="contextType"/>, if present.
    /// </summary>
    /// <param name="contextType">The context type.</param>
    /// <returns>The captured context value, or <see langword="null"/> when not present.</returns>
    object? GetContext(Type contextType);

    /// <summary>
    /// Gets a captured context value of the specified <paramref name="contextType"/>
    /// for the specified domain, if present.
    /// </summary>
    /// <param name="domain">The domain to read from.</param>
    /// <param name="contextType">The context type.</param>
    /// <returns>The captured context value, or <see langword="null"/> when not present.</returns>
    object? GetContext(string domain, Type contextType);

    /// <summary>
    /// Activates this snapshot for the current execution flow and returns a disposable boundary
    /// that restores the previous ambient state when disposed.
    /// </summary>
    /// <returns>A scope object that restores the previous context state on dispose.</returns>
    IDisposable BeginScope();
}
