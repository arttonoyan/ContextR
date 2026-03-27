namespace ContextR.Internal;

internal sealed class DefaultContextAccessor : IContextAccessor, IContextWriter
{
    private static readonly ContextStorage Storage = new();

    private readonly Func<IServiceProvider, string?>? _domainSelector;
    private readonly IServiceProvider _serviceProvider;

    public DefaultContextAccessor(ContextDomainPolicy domainPolicy, IServiceProvider serviceProvider)
    {
        _domainSelector = domainPolicy.DefaultDomainSelector;
        _serviceProvider = serviceProvider;
    }

    public object? GetContext(Type contextType)
    {
        return Storage.Get(GetDefaultDomain(), contextType);
    }

    public object? GetContext(string domain, Type contextType)
    {
        return Storage.Get(domain, contextType);
    }

    public IContextSnapshot CaptureSnapshot()
    {
        return new ContextSnapshot(CaptureCurrentValues(), GetDefaultDomain());
    }

    public IContextSnapshot CreateSnapshot(Type contextType, object context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var defaultDomain = GetDefaultDomain();
        return new ContextSnapshot(new Dictionary<ContextKey, object>
        {
            [new ContextKey(defaultDomain, contextType)] = context
        }, defaultDomain);
    }

    public IContextSnapshot CreateSnapshot(string domain, Type contextType, object context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var defaultDomain = GetDefaultDomain();
        return new ContextSnapshot(new Dictionary<ContextKey, object>
        {
            [new ContextKey(domain, contextType)] = context
        }, defaultDomain);
    }

    public void SetContext(Type contextType, object? context)
    {
        Storage.Set(GetDefaultDomain(), contextType, context);
    }

    public void SetContext(string domain, Type contextType, object? context)
    {
        Storage.Set(domain, contextType, context);
    }

    public void ClearContext(Type contextType)
    {
        Storage.Set(GetDefaultDomain(), contextType, null);
    }

    public void ClearContext(string domain, Type contextType)
    {
        Storage.Set(domain, contextType, null);
    }

    private string? GetDefaultDomain() => _domainSelector?.Invoke(_serviceProvider);

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
