using ContextR.Internal;

namespace ContextR;

/// <summary>
/// Extension methods for capturing immutable context snapshots.
/// </summary>
public static class ContextSnapshotExtensions
{
    /// <summary>
    /// Captures a snapshot of all current ambient context values (across all domains).
    /// </summary>
    /// <param name="accessor">The context accessor.</param>
    /// <returns>An immutable snapshot of current ambient context values.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="accessor"/> is <see langword="null"/>.
    /// </exception>
    public static IContextSnapshot CreateSnapshot(this IContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        var defaultDomain = (accessor as MutableContextAccessor)?.DefaultDomain;
        return new ContextSnapshot(MutableContextAccessor.CaptureCurrentValues(), defaultDomain);
    }

    /// <summary>
    /// Creates a snapshot containing only the provided context value in the default domain,
    /// without touching ambient state.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="accessor">The context accessor.</param>
    /// <param name="context">The context value to include in the snapshot.</param>
    /// <returns>An immutable snapshot containing only <paramref name="context"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="accessor"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public static IContextSnapshot CreateSnapshot<TContext>(this IContextAccessor accessor, TContext context)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(context);

        var defaultDomain = (accessor as MutableContextAccessor)?.DefaultDomain;
        return new ContextSnapshot(new Dictionary<ContextKey, object>
        {
            [new ContextKey(defaultDomain, typeof(TContext))] = context
        }, defaultDomain);
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
    /// Thrown when <paramref name="accessor"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public static IContextSnapshot CreateSnapshot<TContext>(this IContextAccessor accessor, string domain, TContext context)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(context);

        var defaultDomain = (accessor as MutableContextAccessor)?.DefaultDomain;
        return new ContextSnapshot(new Dictionary<ContextKey, object>
        {
            [new ContextKey(domain, typeof(TContext))] = context
        }, defaultDomain);
    }
}
