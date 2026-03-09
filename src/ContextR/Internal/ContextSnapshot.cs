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

    public object? GetContext(Type contextType)
    {
        return _values.TryGetValue(new ContextKey(_defaultDomain, contextType), out var value)
            ? value
            : null;
    }

    public object? GetContext(string domain, Type contextType)
    {
        return _values.TryGetValue(new ContextKey(domain, contextType), out var value)
            ? value
            : null;
    }

    public IDisposable BeginScope()
    {
        return new ContextScope(_values);
    }
}
