namespace ContextR;

internal sealed class ContextBuilder : IContextBuilder
{
    private readonly HashSet<Type> _registeredTypes = [];

    public IContextBuilder Add<TContext>(Action<IContextRegistrationBuilder<TContext>>? configure = null)
        where TContext : class
    {
        _registeredTypes.Add(typeof(TContext));
        configure?.Invoke(new ContextRegistrationBuilder<TContext>());
        return this;
    }
}
