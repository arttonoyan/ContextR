namespace ContextR;

/// <summary>
/// Extension methods for required context access.
/// </summary>
public static class ContextRequiredExtensions
{
    /// <summary>
    /// Gets the ambient context value of type <typeparamref name="TContext"/> and throws when missing.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="accessor">The context accessor.</param>
    /// <returns>The current context value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessor"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the required context is not available.</exception>
    public static TContext GetRequiredContext<TContext>(this IContextAccessor accessor)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(accessor);

        return accessor.GetContext<TContext>()
            ?? throw new InvalidOperationException(
                $"Required context '{typeof(TContext).FullName}' is not available in the current ambient context.");
    }

    /// <summary>
    /// Gets the captured context value of type <typeparamref name="TContext"/> and throws when missing.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="snapshot">The context snapshot.</param>
    /// <returns>The captured context value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshot"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the required context is not present in the snapshot.</exception>
    public static TContext GetRequiredContext<TContext>(this IContextSnapshot snapshot)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot.GetContext<TContext>()
            ?? throw new InvalidOperationException(
                $"Required context '{typeof(TContext).FullName}' is not available in the captured snapshot.");
    }
}
