namespace ContextR.Resolution.Internal;

/// <summary>
/// Domain-aware registry for context resolution policies.
/// </summary>
/// <typeparam name="TContext">The context type policy is for.</typeparam>
public sealed class ContextResolutionPolicyRegistry<TContext>
    where TContext : class
{
    private const string DefaultDomainKey = "__default__";
    private readonly Dictionary<string, Func<IServiceProvider, IContextResolutionPolicy<TContext>>> _factories =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Adds policy factory for a domain if one is not already registered.
    /// </summary>
    public void TryAdd(string? domain, Func<IServiceProvider, IContextResolutionPolicy<TContext>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factories.TryAdd(NormalizeDomain(domain), factory);
    }

    /// <summary>
    /// Resolves policy for domain, falling back to default domain policy.
    /// </summary>
    public IContextResolutionPolicy<TContext>? Resolve(IServiceProvider services, string? domain)
    {
        ArgumentNullException.ThrowIfNull(services);

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
