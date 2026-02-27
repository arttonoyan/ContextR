namespace ContextR.Internal;

internal sealed class MutableContextAccessor : IContextAccessor, IContextWriter
{
    private static readonly ContextStorage Storage = new();

    public TContext? Get<TContext>() where TContext : class
    {
        return Storage.Get<TContext>();
    }

    public void Set<TContext>(TContext? context) where TContext : class
    {
        Storage.Set(context);
    }

    internal static Dictionary<Type, object> CaptureCurrentValues()
    {
        return Storage.CaptureAll();
    }

    internal static void ApplyValues(IReadOnlyDictionary<Type, object> values)
    {
        foreach (var entry in values)
        {
            Storage.SetRaw(entry.Key, entry.Value);
        }
    }

    internal static void SetRawValue(Type contextType, object? context)
    {
        Storage.SetRaw(contextType, context);
    }
}
