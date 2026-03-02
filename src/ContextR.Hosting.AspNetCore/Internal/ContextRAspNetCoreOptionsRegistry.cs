namespace ContextR.Hosting.AspNetCore.Internal;

internal sealed class ContextRAspNetCoreOptionsRegistry<TContext>
    where TContext : class
{
    private readonly Dictionary<string, Func<IServiceProvider, ContextRAspNetCoreOptions<TContext>>> _byDomain =
        new(StringComparer.OrdinalIgnoreCase);

    private Func<IServiceProvider, ContextRAspNetCoreOptions<TContext>>? _default;

    public void TryAdd(string? domain, Func<IServiceProvider, ContextRAspNetCoreOptions<TContext>> factory)
    {
        if (domain is null)
        {
            _default ??= factory;
            return;
        }

        _byDomain.TryAdd(domain, factory);
    }

    public ContextRAspNetCoreOptions<TContext> Resolve(IServiceProvider serviceProvider, string? domain)
    {
        if (domain is not null && _byDomain.TryGetValue(domain, out var factory))
            return factory(serviceProvider);

        if (_default is not null)
            return _default(serviceProvider);

        return new ContextRAspNetCoreOptions<TContext>();
    }
}
