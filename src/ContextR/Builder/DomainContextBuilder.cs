using Microsoft.Extensions.DependencyInjection;

namespace ContextR;

internal sealed class DomainContextBuilder : IDomainContextBuilder
{
    private readonly string _domain;
    private readonly IServiceCollection _services;
    private readonly HashSet<Type> _registeredTypes = [];

    public DomainContextBuilder(string domain, IServiceCollection services)
    {
        _domain = domain;
        _services = services;
    }

    public IDomainContextBuilder Add<TContext>(Action<IContextRegistrationBuilder<TContext>>? configure = null)
        where TContext : class
    {
        _registeredTypes.Add(typeof(TContext));
        var regBuilder = new ContextRegistrationBuilder<TContext>(_services, _domain);
        configure?.Invoke(regBuilder);
        regBuilder.Build();
        return this;
    }
}
