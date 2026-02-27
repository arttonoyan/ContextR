namespace ContextR.Internal;

internal sealed class ContextSnapshot : IContextSnapshot
{
    internal static readonly IContextSnapshot Empty = new ContextSnapshot([]);

    private readonly Dictionary<Type, object> _values;

    public ContextSnapshot(Dictionary<Type, object> values)
    {
        _values = new Dictionary<Type, object>(values);
    }

    public TContext? Get<TContext>() where TContext : class
    {
        return _values.TryGetValue(typeof(TContext), out var value) ? value as TContext : null;
    }

    public IDisposable BeginScope()
    {
        return new ContextScope(_values);
    }
}
