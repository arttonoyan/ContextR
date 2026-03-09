namespace ContextR;

/// <summary>
/// Generic convenience extensions for <see cref="IContextAccessor"/>,
/// <see cref="IContextSnapshot"/>, and <see cref="IContextWriter"/>.
/// </summary>
public static class ContextExtensions
{
    /// <summary>
    /// Gets the ambient context value of type <typeparamref name="TContext"/>, if present.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="accessor">The context accessor.</param>
    /// <returns>The current context value, or <see langword="null"/> when not set.</returns>
    public static TContext? GetContext<TContext>(this IContextAccessor accessor) where TContext : class
        => accessor.GetContext(typeof(TContext)) as TContext;

    /// <summary>
    /// Gets the ambient context value of type <typeparamref name="TContext"/> for the specified domain, if present.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="accessor">The context accessor.</param>
    /// <param name="domain">The domain to read from.</param>
    /// <returns>The current context value, or <see langword="null"/> when not set.</returns>
    public static TContext? GetContext<TContext>(this IContextAccessor accessor, string domain) where TContext : class
        => accessor.GetContext(domain, typeof(TContext)) as TContext;

    /// <summary>
    /// Creates a snapshot containing only the provided context value in the default domain,
    /// without touching ambient state.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="accessor">The context accessor.</param>
    /// <param name="context">The context value to include in the snapshot.</param>
    /// <returns>An immutable snapshot containing only <paramref name="context"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public static IContextSnapshot CreateSnapshot<TContext>(this IContextAccessor accessor, TContext context) where TContext : class
    {
        ArgumentNullException.ThrowIfNull(context);
        return accessor.CreateSnapshot(typeof(TContext), context);
    }

    /// <summary>
    /// Creates a snapshot containing only the provided context value for the specified domain,
    /// without touching ambient state.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="accessor">The context accessor.</param>
    /// <param name="domain">The domain to associate the context value with.</param>
    /// <param name="context">The context value to include in the snapshot.</param>
    /// <returns>An immutable snapshot containing only <paramref name="context"/> for <paramref name="domain"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public static IContextSnapshot CreateSnapshot<TContext>(this IContextAccessor accessor, string domain, TContext context) where TContext : class
    {
        ArgumentNullException.ThrowIfNull(context);
        return accessor.CreateSnapshot(domain, typeof(TContext), context);
    }

    /// <summary>
    /// Gets a captured context value of type <typeparamref name="TContext"/>, if present.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="snapshot">The context snapshot.</param>
    /// <returns>The captured context value, or <see langword="null"/> when not present.</returns>
    public static TContext? GetContext<TContext>(this IContextSnapshot snapshot) where TContext : class
        => snapshot.GetContext(typeof(TContext)) as TContext;

    /// <summary>
    /// Gets a captured context value of type <typeparamref name="TContext"/> for the specified domain, if present.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="snapshot">The context snapshot.</param>
    /// <param name="domain">The domain to read from.</param>
    /// <returns>The captured context value, or <see langword="null"/> when not present.</returns>
    public static TContext? GetContext<TContext>(this IContextSnapshot snapshot, string domain) where TContext : class
        => snapshot.GetContext(domain, typeof(TContext)) as TContext;

    /// <summary>
    /// Sets the ambient context value of type <typeparamref name="TContext"/>.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="writer">The context writer.</param>
    /// <param name="context">
    /// The value to set, or <see langword="null"/> to clear according to writer semantics.
    /// </param>
    public static void SetContext<TContext>(this IContextWriter writer, TContext? context) where TContext : class
        => writer.SetContext(typeof(TContext), context);

    /// <summary>
    /// Sets the ambient context value of type <typeparamref name="TContext"/> for the specified domain.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="writer">The context writer.</param>
    /// <param name="domain">The domain to write to.</param>
    /// <param name="context">
    /// The value to set, or <see langword="null"/> to clear according to writer semantics.
    /// </param>
    public static void SetContext<TContext>(this IContextWriter writer, string domain, TContext? context) where TContext : class
        => writer.SetContext(domain, typeof(TContext), context);
}
