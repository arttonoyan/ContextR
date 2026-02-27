namespace ContextR.Internal;

internal sealed class ContextScope : IDisposable
{
    private readonly Dictionary<ContextKey, object?> _previousValues;
    private readonly IReadOnlyCollection<ContextKey> _appliedKeys;
    private bool _disposed;

    public ContextScope(IReadOnlyDictionary<ContextKey, object> nextValues)
    {
        var current = DefaultContextAccessor.CaptureCurrentValues();
        _appliedKeys = nextValues.Keys.ToList();
        _previousValues = new Dictionary<ContextKey, object?>(_appliedKeys.Count);

        foreach (var key in _appliedKeys)
        {
            _previousValues[key] = current.TryGetValue(key, out var previous)
                ? previous
                : null;
        }

        DefaultContextAccessor.ApplyValues(nextValues);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!disposing || _disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var key in _appliedKeys)
        {
            _previousValues.TryGetValue(key, out var previous);
            DefaultContextAccessor.SetRawValue(key, previous);
        }
    }
}
