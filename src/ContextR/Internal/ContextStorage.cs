using System.Collections.Concurrent;

namespace ContextR.Internal;

internal sealed class ContextStorage
{
    private readonly ConcurrentDictionary<ContextKey, AsyncLocal<ContextHolder?>> _storage = new();

    public TContext? Get<TContext>(string? domain) where TContext : class
    {
        return GetSlot(new ContextKey(domain, typeof(TContext))).Value?.Context as TContext;
    }

    public void Set<TContext>(string? domain, TContext? context) where TContext : class
    {
        var key = new ContextKey(domain, typeof(TContext));
        var slot = GetSlot(key);
        slot.Value = context is not null ? 
            new ContextHolder { Context = context } : 
            null;
    }

    public Dictionary<ContextKey, object> CaptureAll()
    {
        var result = new Dictionary<ContextKey, object>();

        foreach (var entry in _storage)
        {
            var value = entry.Value.Value?.Context;
            if (value is not null)
            {
                result[entry.Key] = value;
            }
        }

        return result;
    }

    public void SetRaw(ContextKey key, object? context)
    {
        var slot = GetSlot(key);
        slot.Value = context is null ? null : new ContextHolder { Context = context };
    }

    private AsyncLocal<ContextHolder?> GetSlot(ContextKey key)
    {
        return _storage.GetOrAdd(key, static _ => new AsyncLocal<ContextHolder?>());
    }
}
