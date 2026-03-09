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

    public TContext? GetContext<TContext>() where TContext : class
    {
        return Storage.Get<TContext>(GetDefaultDomain());
    }

    public TContext? GetContext<TContext>(string domain) where TContext : class
    {
        return Storage.Get<TContext>(domain);
    }

    public IContextSnapshot CreateSnapshot()
    {
        return new ContextSnapshot(CaptureCurrentValues(), GetDefaultDomain());
    }

    public IContextSnapshot CreateSnapshot<TContext>(TContext context) where TContext : class
    {
        ArgumentNullException.ThrowIfNull(context);

        var defaultDomain = GetDefaultDomain();
        return new ContextSnapshot(new Dictionary<ContextKey, object>
        {
            [new ContextKey(defaultDomain, typeof(TContext))] = context
        }, defaultDomain);
    }

    public IContextSnapshot CreateSnapshot<TContext>(string domain, TContext context) where TContext : class
    {
        ArgumentNullException.ThrowIfNull(context);

        var defaultDomain = GetDefaultDomain();
        return new ContextSnapshot(new Dictionary<ContextKey, object>
        {
            [new ContextKey(domain, typeof(TContext))] = context
        }, defaultDomain);
    }

    public void SetContext<TContext>(TContext? context) where TContext : class
    {
        Storage.Set(GetDefaultDomain(), context);
    }

    public void SetContext<TContext>(string domain, TContext? context) where TContext : class
    {
        Storage.Set(domain, context);
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
