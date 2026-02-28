namespace ContextR.Propagation.Internal;

internal sealed class MappingContextPropagator<TContext> : IContextPropagator<TContext>
    where TContext : class
{
    private readonly IPropertyMapping<TContext>[] _mappings;
    private readonly IContextPropagationFailureHandler<TContext>? _failureHandler;
    private readonly string? _domain;

    public MappingContextPropagator(
        IEnumerable<IPropertyMapping<TContext>> mappings,
        IContextPropagationFailureHandler<TContext>? failureHandler = null,
        string? domain = null)
    {
        if (!HasParameterlessConstructor())
        {
            throw new InvalidOperationException(
                $"Context type '{typeof(TContext).FullName}' must have a public parameterless constructor " +
                "to use MapProperty. Either add a parameterless constructor or use UsePropagator instead.");
        }

        _mappings = mappings.ToArray();
        _failureHandler = failureHandler;
        _domain = domain;
    }

    public void Inject<TCarrier>(TContext context, TCarrier carrier, Action<TCarrier, string, string> setter)
    {
        foreach (var mapping in _mappings)
        {
            string? value;
            try
            {
                value = mapping.GetValue(context);
            }
            catch (PropertyMappingException ex)
            {
                if (HandleFailure(
                        mapping.Key,
                        PropagationDirection.Inject,
                        ex.Reason,
                        null,
                        ex) == PropagationFailureAction.SkipContext)
                {
                    return;
                }

                continue;
            }

            if (value is null)
            {
                if (mapping.IsRequired)
                {
                    if (HandleFailure(
                            mapping.Key,
                            PropagationDirection.Inject,
                            PropagationFailureReason.MissingRequired) == PropagationFailureAction.SkipContext)
                    {
                        return;
                    }
                }

                continue;
            }

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
            if (value is null)
            {
                if (mapping.IsRequired)
                {
                    if (HandleFailure(
                            mapping.Key,
                            PropagationDirection.Extract,
                            PropagationFailureReason.MissingRequired) == PropagationFailureAction.SkipContext)
                    {
                        return null;
                    }
                }

                continue;
            }

            bool parsed;
            try
            {
                parsed = mapping.TrySetValue(context, value);
            }
            catch (PropertyMappingException ex)
            {
                if (HandleFailure(
                        mapping.Key,
                        PropagationDirection.Extract,
                        ex.Reason,
                        value,
                        ex) == PropagationFailureAction.SkipContext)
                {
                    return null;
                }

                continue;
            }

            if (parsed)
            {
                anySet = true;
                continue;
            }

            if (mapping.IsRequired)
            {
                if (HandleFailure(
                        mapping.Key,
                        PropagationDirection.Extract,
                        PropagationFailureReason.ParseFailed,
                        value) == PropagationFailureAction.SkipContext)
                {
                    return null;
                }
            }
        }

        return anySet ? context : null;
    }

    private static bool HasParameterlessConstructor()
    {
        return typeof(TContext).GetConstructor(Type.EmptyTypes) is not null;
    }

    private PropagationFailureAction HandleFailure(
        string key,
        PropagationDirection direction,
        PropagationFailureReason reason,
        string? rawValue = null,
        Exception? exception = null)
    {
        var failure = new PropagationFailureContext
        {
            ContextType = typeof(TContext),
            Key = key,
            Direction = direction,
            Reason = reason,
            Domain = _domain,
            RawValue = rawValue,
            Exception = exception
        };

        var action = _failureHandler?.Handle(failure) ?? PropagationFailureAction.Throw;

        if (action == PropagationFailureAction.Throw)
        {
            if (exception is not null)
                throw exception;

            throw direction switch
            {
                PropagationDirection.Inject => new InvalidOperationException(
                    $"Context propagation inject failed for key '{key}' with reason '{reason}'."),
                _ => new InvalidOperationException(
                    $"Context propagation extract failed for key '{key}' with reason '{reason}'.")
            };
        }

        return action;
    }
}
