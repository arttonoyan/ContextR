namespace ContextR.Internal;

internal sealed class ContextSnapshot : IContextSnapshot
{
    private readonly Dictionary<ContextKey, object> _values;
    private readonly string? _defaultDomain;

    public ContextSnapshot(Dictionary<ContextKey, object> values, string? defaultDomain)
    {
        _values = new Dictionary<ContextKey, object>(values);
        _defaultDomain = defaultDomain;
    }

    public TContext? GetContext<TContext>() where TContext : class
    {
        return _values.TryGetValue(new ContextKey(_defaultDomain, typeof(TContext)), out var value)
            ? value as TContext
            : null;
    }

    public TContext? GetContext<TContext>(string domain) where TContext : class
    {
        return _values.TryGetValue(new ContextKey(domain, typeof(TContext)), out var value)
            ? value as TContext
            : null;
    }

    public IDisposable BeginScope()
    {
        return new ContextScope(_values);
    }
}
