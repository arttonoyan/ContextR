namespace ContextR;

/// <summary>
/// Represents an immutable captured state of ambient contexts.
/// </summary>
public interface IContextSnapshot
{
    /// <summary>
    /// Gets a captured context value of type <typeparamref name="TContext"/>, if present.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <returns>The captured context value, or <see langword="null"/> when not present.</returns>
    TContext? GetContext<TContext>() where TContext : class;

    /// <summary>
    /// Activates this snapshot for the current execution flow and returns a disposable boundary
    /// that restores the previous ambient state when disposed.
    /// </summary>
    /// <returns>A scope object that restores the previous context state on dispose.</returns>
    IDisposable BeginScope();
}
