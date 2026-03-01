namespace ContextR.Propagation;

/// <summary>
/// Domain-aware registry for strategy policies.
/// </summary>
public sealed class ContextPropagationStrategyPolicyRegistry<TContext>
    where TContext : class
{
    private const string DefaultDomainKey = "__default__";
    private readonly Dictionary<string, Func<IServiceProvider, IContextPropagationStrategyPolicy<TContext>>> _factories =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Adds policy factory for a domain if one is not already registered.
    /// </summary>
    public void TryAdd(string? domain, Func<IServiceProvider, IContextPropagationStrategyPolicy<TContext>> factory)
    {
        var key = NormalizeDomain(domain);
        _factories.TryAdd(key, factory);
    }

    /// <summary>
    /// Resolves policy for domain, falling back to default domain policy.
    /// </summary>
    public IContextPropagationStrategyPolicy<TContext>? Resolve(IServiceProvider services, string? domain)
    {
        var domainKey = NormalizeDomain(domain);

        if (_factories.TryGetValue(domainKey, out var domainFactory))
            return domainFactory(services);

        if (_factories.TryGetValue(DefaultDomainKey, out var defaultFactory))
            return defaultFactory(services);

        return null;
    }

    private static string NormalizeDomain(string? domain)
    {
        return string.IsNullOrEmpty(domain) ? DefaultDomainKey : domain;
    }
}
