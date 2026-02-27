using Microsoft.Extensions.DependencyInjection;

namespace ContextR;

internal sealed class ContextBuilder : IContextBuilder
{
    private readonly HashSet<Type> _registeredTypes = [];

    private bool _hasDefaultRegistrations;
    private bool _hasDomainRegistrations;

    public ContextBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }

    internal ContextDomainPolicy DomainPolicy { get; } = new();

    public IContextBuilder Add<TContext>(Action<IContextRegistrationBuilder<TContext>>? configure = null)
        where TContext : class
    {
        _registeredTypes.Add(typeof(TContext));
        _hasDefaultRegistrations = true;
        var regBuilder = new ContextRegistrationBuilder<TContext>(Services, domain: null);
        configure?.Invoke(regBuilder);
        regBuilder.Build();
        return this;
    }

    public IContextBuilder AddDomain(string domain, Action<IDomainContextBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(configure);

        _hasDomainRegistrations = true;
        var domainBuilder = new DomainContextBuilder(domain, Services);
        configure(domainBuilder);
        return this;
    }

    public IContextBuilder AddDomainPolicy(Action<ContextDomainPolicy> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(DomainPolicy);
        return this;
    }

    internal void Validate()
    {
        if (_hasDomainRegistrations && !_hasDefaultRegistrations && DomainPolicy.DefaultDomainSelector is null)
        {
            throw new InvalidOperationException(
                "Domain registrations were configured but no default (domainless) registration exists " +
                "and no DefaultDomainSelector was provided via AddDomainPolicy. " +
                "Either call Add<TContext>() at the root level to register a default, " +
                "or configure AddDomainPolicy(p => p.DefaultDomainSelector = ...) to select a default domain.");
        }
    }
}
