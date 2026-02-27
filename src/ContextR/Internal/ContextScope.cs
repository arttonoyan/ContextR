namespace ContextR.Internal;

internal sealed class ContextScope : IDisposable
{
    private readonly Dictionary<Type, object?> _previousValues;
    private readonly IReadOnlyCollection<Type> _appliedTypes;
    private bool _disposed;

    public ContextScope(IReadOnlyDictionary<Type, object> nextValues)
    {
        var current = MutableContextAccessor.CaptureCurrentValues();
        _appliedTypes = nextValues.Keys.ToList();
        _previousValues = new Dictionary<Type, object?>(_appliedTypes.Count);

        foreach (var contextType in _appliedTypes)
        {
            _previousValues[contextType] = current.TryGetValue(contextType, out var previous)
                ? previous
                : null;
        }

        MutableContextAccessor.ApplyValues(nextValues);
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
        foreach (var contextType in _appliedTypes)
        {
            _previousValues.TryGetValue(contextType, out var previous);
            MutableContextAccessor.SetRawValue(contextType, previous);
        }
    }
}
