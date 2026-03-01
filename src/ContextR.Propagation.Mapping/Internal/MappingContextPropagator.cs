namespace ContextR.Propagation.Internal;

internal sealed class MappingContextPropagator<TContext> : IContextPropagator<TContext>
    where TContext : class
{
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private static readonly IServiceProvider NoServices = new EmptyServiceProvider();
    private readonly IPropertyMapping<TContext>[] _mappings;
    private readonly IServiceProvider _services;
    private readonly ContextPropagationFailureHandlerRegistry<TContext>? _failureRegistry;
    private readonly IPropagationExecutionScope _executionScope;

    public MappingContextPropagator(
        IEnumerable<IPropertyMapping<TContext>> mappings,
        IServiceProvider? services = null,
        IPropagationExecutionScope? executionScope = null,
        ContextPropagationFailureHandlerRegistry<TContext>? failureRegistry = null)
    {
        if (!HasParameterlessConstructor())
        {
            throw new InvalidOperationException(
                $"Context type '{typeof(TContext).FullName}' must have a public parameterless constructor " +
                "to use MapProperty. Either add a parameterless constructor or use UsePropagator instead.");
        }

        _mappings = mappings.ToArray();
        _services = services ?? NoServices;
        _executionScope = executionScope ?? new AsyncLocalPropagationExecutionScope();
        _failureRegistry = failureRegistry;
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
            Domain = _executionScope.CurrentDomain,
            RawValue = rawValue,
            Exception = exception
        };

        var failureHandler = _failureRegistry?.Resolve(_services, failure.Domain);
        var action = failureHandler?.Handle(failure) ?? PropagationFailureAction.Throw;

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
