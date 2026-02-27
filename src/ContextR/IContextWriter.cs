namespace ContextR;

/// <summary>
/// Writes ambient context values for the current asynchronous flow.
/// </summary>
public interface IContextWriter
{
    /// <summary>
    /// Sets the ambient context value of type <typeparamref name="TContext"/>.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="context">
    /// The value to set, or <see langword="null"/> to clear according to writer semantics.
    /// </param>
    void SetContext<TContext>(TContext? context) where TContext : class;

    /// <summary>
    /// Sets the ambient context value of type <typeparamref name="TContext"/> for the specified domain.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="domain">The domain to write to.</param>
    /// <param name="context">
    /// The value to set, or <see langword="null"/> to clear according to writer semantics.
    /// </param>
    void SetContext<TContext>(string domain, TContext? context) where TContext : class;
}
