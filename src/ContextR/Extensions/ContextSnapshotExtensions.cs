using ContextR.Internal;

namespace ContextR;

/// <summary>
/// Extension methods for capturing immutable context snapshots.
/// </summary>
public static class ContextSnapshotExtensions
{
    /// <summary>
    /// Captures the current ambient context values into an immutable snapshot.
    /// </summary>
    /// <param name="accessor">The context accessor.</param>
    /// <returns>An immutable snapshot of current ambient context values.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="accessor"/> is <see langword="null"/>.
    /// </exception>
    public static IContextSnapshot Capture(this IContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        return new ContextSnapshot(MutableContextAccessor.CaptureCurrentValues());
    }

    /// <summary>
    /// Creates an immutable snapshot containing only the provided context value.
    /// This method does not read from or write to ambient storage.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="accessor">The context accessor.</param>
    /// <param name="context">The context value to capture.</param>
    /// <returns>An immutable snapshot containing the provided context value.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="accessor"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public static IContextSnapshot Capture<TContext>(this IContextAccessor accessor, TContext context)
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
