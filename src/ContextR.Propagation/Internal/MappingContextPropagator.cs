namespace ContextR.Propagation.Internal;

internal sealed class MappingContextPropagator<TContext> : IContextPropagator<TContext>
    where TContext : class
{
    private readonly IPropertyMapping<TContext>[] _mappings;

    public MappingContextPropagator(IEnumerable<IPropertyMapping<TContext>> mappings)
    {
        if (!HasParameterlessConstructor())
        {
            throw new InvalidOperationException(
                $"Context type '{typeof(TContext).FullName}' must have a public parameterless constructor " +
                "to use MapProperty. Either add a parameterless constructor or use UsePropagator instead.");
        }

        _mappings = mappings.ToArray();
    }

    public void Inject<TCarrier>(TContext context, TCarrier carrier, Action<TCarrier, string, string> setter)
    {
        foreach (var mapping in _mappings)
        {
            var value = mapping.GetValue(context);
            if (value is not null)
                setter(carrier, mapping.Key, value);
        }
    }

    public TContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, string?> getter)
    {
        var context = Activator.CreateInstance<TContext>();
        var anySet = false;

        foreach (var mapping in _mappings)
        {
            var value = getter(carrier, mapping.Key);
            if (value is not null && mapping.TrySetValue(context, value))
                anySet = true;
        }

        return anySet ? context : null;
    }

    private static bool HasParameterlessConstructor()
    {
        return typeof(TContext).GetConstructor(Type.EmptyTypes) is not null;
    }
}
