namespace ContextR.DependencyInjection;

internal sealed class ContextRBuilder : IContextRBuilder
{
    private readonly HashSet<Type> _registeredTypes = [];

    public IContextRBuilder Add<TContext>(Action<IContextBuilder<TContext>>? configure = null)
        where TContext : class
    {
        _registeredTypes.Add(typeof(TContext));
        configure?.Invoke(new ContextBuilder<TContext>());
        return this;
    }
}
