namespace ContextR.Resolution.Internal;

/// <summary>
/// Domain-aware registry for context resolvers.
/// </summary>
/// <typeparam name="TContext">The context type resolver is for.</typeparam>
public sealed class ContextResolverRegistry<TContext>
    where TContext : class
{
    private const string DefaultDomainKey = "__default__";
    private readonly Dictionary<string, Func<IServiceProvider, IContextResolver<TContext>>> _factories =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Adds resolver factory for a domain if one is not already registered.
    /// </summary>
    public void TryAdd(string? domain, Func<IServiceProvider, IContextResolver<TContext>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factories.TryAdd(NormalizeDomain(domain), factory);
    }

    /// <summary>
    /// Resolves resolver for domain, falling back to default domain resolver.
    /// </summary>
    public IContextResolver<TContext>? Resolve(IServiceProvider services, string? domain)
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
