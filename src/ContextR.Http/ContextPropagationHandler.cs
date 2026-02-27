namespace ContextR.Http;

/// <summary>
/// A <see cref="DelegatingHandler"/> that propagates the current ambient context
/// to outgoing HTTP requests by injecting key-value pairs into request headers
/// via the registered <see cref="IContextPropagator{TContext}"/>.
/// </summary>
/// <typeparam name="TContext">The context type to propagate.</typeparam>
public class ContextPropagationHandler<TContext> : DelegatingHandler
    where TContext : class
{
    private readonly IContextAccessor _accessor;
    private readonly IContextPropagator<TContext> _propagator;

    /// <inheritdoc cref="ContextPropagationHandler{TContext}" />
    public ContextPropagationHandler(IContextAccessor accessor, IContextPropagator<TContext> propagator)
    {
        _accessor = accessor;
        _propagator = propagator;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var context = _accessor.GetContext<TContext>();
        if (context is not null)
        {
            _propagator.Inject(context, request.Headers,
                static (headers, key, value) => headers.TryAddWithoutValidation(key, value));
        }

        return base.SendAsync(request, cancellationToken);
    }
}
