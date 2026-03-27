namespace ContextR;

/// <summary>
/// Writes ambient context values for the current asynchronous flow.
/// </summary>
public interface IContextWriter
{
    /// <summary>
    /// Sets the ambient context value of the specified <paramref name="contextType"/>.
    /// </summary>
    /// <param name="contextType">The context type.</param>
    /// <param name="context">
    /// The value to set, or <see langword="null"/> to clear according to writer semantics.
    /// </param>
    void SetContext(Type contextType, object? context);

    /// <summary>
    /// Sets the ambient context value of the specified <paramref name="contextType"/>
    /// for the specified domain.
    /// </summary>
    /// <param name="domain">The domain to write to.</param>
    /// <param name="contextType">The context type.</param>
    /// <param name="context">
    /// The value to set, or <see langword="null"/> to clear according to writer semantics.
    /// </param>
    void SetContext(string domain, Type contextType, object? context);

    /// <summary>
    /// Clears the ambient context value of the specified <paramref name="contextType"/>.
    /// </summary>
    /// <param name="contextType">The context type to clear.</param>
    void ClearContext(Type contextType);

    /// <summary>
    /// Clears the ambient context value of the specified <paramref name="contextType"/>
    /// for the specified domain.
    /// </summary>
    /// <param name="domain">The domain to clear from.</param>
    /// <param name="contextType">The context type to clear.</param>
    void ClearContext(string domain, Type contextType);
}
