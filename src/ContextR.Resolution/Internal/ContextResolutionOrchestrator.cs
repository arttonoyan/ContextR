namespace ContextR.Resolution.Internal;

/// <summary>
/// Default orchestrator that resolves context from propagated + resolver sources and applies resolution policy.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
public sealed class ContextResolutionOrchestrator<TContext> : IContextResolutionOrchestrator<TContext>
    where TContext : class
{
    private readonly IServiceProvider _services;
    private readonly IContextWriter _writer;
    private readonly IContextResolutionPolicy<TContext> _defaultPolicy;
    private readonly ContextResolverRegistry<TContext>? _resolverRegistry;
    private readonly ContextResolutionPolicyRegistry<TContext>? _policyRegistry;

    /// <summary>
    /// Creates a new resolution orchestrator.
    /// </summary>
    public ContextResolutionOrchestrator(
        IServiceProvider services,
        IContextWriter writer,
        IContextResolutionPolicy<TContext> defaultPolicy,
        ContextResolverRegistry<TContext>? resolverRegistry = null,
        ContextResolutionPolicyRegistry<TContext>? policyRegistry = null)
    {
        _services = services;
        _writer = writer;
        _defaultPolicy = defaultPolicy;
        _resolverRegistry = resolverRegistry;
        _policyRegistry = policyRegistry;
    }

    /// <inheritdoc />
    public ContextResolutionResult<TContext> Resolve(ContextResolutionContext context, TContext? propagatedContext = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var resolver = _resolverRegistry?.Resolve(_services, context.Domain);
        var resolvedContext = resolver?.Resolve(context);

        var policy = _policyRegistry?.Resolve(_services, context.Domain) ?? _defaultPolicy;
        return policy.Resolve(new ContextResolutionPolicyContext<TContext>
        {
            ResolutionContext = context,
            ResolvedContext = resolvedContext,
            PropagatedContext = propagatedContext
        });
    }

    /// <inheritdoc />
    public ContextResolutionResult<TContext> ResolveAndWrite(ContextResolutionContext context, TContext? propagatedContext = null)
    {
        var result = Resolve(context, propagatedContext);
        if (result.Context is null)
            return result;

        if (string.IsNullOrEmpty(context.Domain))
            _writer.SetContext(result.Context);
        else
            _writer.SetContext(context.Domain, result.Context);

        return result;
    }
}
