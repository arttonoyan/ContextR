using ContextR.Internal;

namespace ContextR;

/// <summary>
/// Extension methods for capturing immutable context snapshots.
/// </summary>
public static class ContextSnapshotExtensions
{
    /// <summary>
    /// Captures a snapshot of all current ambient context values.
    /// </summary>
    /// <param name="accessor">The context accessor.</param>
    /// <returns>An immutable snapshot of current ambient context values.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="accessor"/> is <see langword="null"/>.
    /// </exception>
    public static IContextSnapshot CreateSnapshot(this IContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        return new ContextSnapshot(MutableContextAccessor.CaptureCurrentValues());
    }

    /// <summary>
    /// Creates a snapshot containing only the provided context value, without touching ambient state.
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

        return new ContextSnapshot(new Dictionary<Type, object>
        {
            [typeof(TContext)] = context
        });
    }
}
