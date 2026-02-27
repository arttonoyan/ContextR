using System.Collections.Concurrent;

namespace ContextR.Internal;

internal sealed class ContextStorage
{
    private readonly ConcurrentDictionary<Type, AsyncLocal<ContextHolder?>> _storage = new();

    public TContext? Get<TContext>() where TContext : class
    {
        return GetSlot(typeof(TContext)).Value?.Context as TContext;
    }

    public void Set<TContext>(TContext? context) where TContext : class
    {
        var slot = GetSlot(typeof(TContext));
        var holder = slot.Value;

        if (holder is not null)
        {
            // Clearing the shared holder prevents stale values from leaking across flows
            // that still reference the same holder instance.
            holder.Context = null;
        }

        if (context is not null)
        {
            slot.Value = new ContextHolder { Context = context };
        }
    }

    public Dictionary<Type, object> CaptureAll()
    {
        var result = new Dictionary<Type, object>();

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

    public void SetRaw(Type contextType, object? context)
    {
        var slot = GetSlot(contextType);
        slot.Value = context is null ? null : new ContextHolder { Context = context };
    }

    private AsyncLocal<ContextHolder?> GetSlot(Type contextType)
    {
        return _storage.GetOrAdd(contextType, static _ => new AsyncLocal<ContextHolder?>());
    }
}
