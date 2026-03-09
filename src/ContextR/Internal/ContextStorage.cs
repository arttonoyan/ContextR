using System.Collections.Concurrent;

namespace ContextR.Internal;

internal sealed class ContextStorage
{
    private readonly ConcurrentDictionary<ContextKey, AsyncLocal<ContextHolder?>> _storage = new();

    public object? Get(string? domain, Type contextType)
    {
        return GetSlot(new ContextKey(domain, contextType)).Value?.Context;
    }

    public TContext? Get<TContext>(string? domain) where TContext : class
    {
        return Get(domain, typeof(TContext)) as TContext;
    }

    public void Set(string? domain, Type contextType, object? context)
    {
        SetRaw(new ContextKey(domain, contextType), context);
    }

    public void Set<TContext>(string? domain, TContext? context) where TContext : class
    {
        Set(domain, typeof(TContext), context);
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
