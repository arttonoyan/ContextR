namespace ContextR;

internal sealed class DomainContextBuilder : IDomainContextBuilder
{
    private readonly string _domain;
    private readonly HashSet<Type> _registeredTypes = [];

    public DomainContextBuilder(string domain)
    {
        _domain = domain;
    }

    public IDomainContextBuilder Add<TContext>(Action<IContextRegistrationBuilder<TContext>>? configure = null)
        where TContext : class
    {
        _registeredTypes.Add(typeof(TContext));
        configure?.Invoke(new ContextRegistrationBuilder<TContext>());
        return this;
    }
}
