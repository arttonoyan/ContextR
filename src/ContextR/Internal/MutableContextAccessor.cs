namespace ContextR.Internal;

internal sealed class MutableContextAccessor : IContextAccessor, IContextWriter
{
    private static readonly ContextStorage Storage = new();

    private readonly string? _defaultDomain;

    public MutableContextAccessor(ContextDomainPolicy domainPolicy, IServiceProvider serviceProvider)
    {
        _defaultDomain = domainPolicy.DefaultDomainSelector?.Invoke(serviceProvider);
    }

    internal string? DefaultDomain => _defaultDomain;

    public TContext? GetContext<TContext>() where TContext : class
    {
        return Storage.Get<TContext>(_defaultDomain);
    }

    public TContext? GetContext<TContext>(string domain) where TContext : class
    {
        return Storage.Get<TContext>(domain);
    }

    public void SetContext<TContext>(TContext? context) where TContext : class
    {
        Storage.Set(_defaultDomain, context);
    }

    public void SetContext<TContext>(string domain, TContext? context) where TContext : class
    {
        Storage.Set(domain, context);
    }

    internal static Dictionary<ContextKey, object> CaptureCurrentValues()
    {
        return Storage.CaptureAll();
    }

    internal static void ApplyValues(IReadOnlyDictionary<ContextKey, object> values)
    {
        foreach (var entry in values)
        {
            Storage.SetRaw(entry.Key, entry.Value);
        }
    }

    internal static void SetRawValue(ContextKey key, object? context)
    {
        Storage.SetRaw(key, context);
    }
}
