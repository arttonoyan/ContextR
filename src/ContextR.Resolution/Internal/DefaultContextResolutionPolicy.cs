namespace ContextR.Resolution.Internal;

/// <summary>
/// Default trust-boundary-aware resolution policy.
/// External ingress prefers resolver; internal ingress prefers propagated context.
/// </summary>
/// <typeparam name="TContext">The context type being resolved.</typeparam>
public sealed class DefaultContextResolutionPolicy<TContext> : IContextResolutionPolicy<TContext>
    where TContext : class
{
    /// <inheritdoc />
    public ContextResolutionResult<TContext> Resolve(ContextResolutionPolicyContext<TContext> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var resolved = context.ResolvedContext;
        var propagated = context.PropagatedContext;

        if (resolved is null && propagated is null)
            return ContextResolutionResult<TContext>.None();

        if (resolved is null)
        {
            return new ContextResolutionResult<TContext>
            {
                Context = propagated,
                Source = ContextResolutionSource.Propagated
            };
        }

        if (propagated is null)
        {
            return new ContextResolutionResult<TContext>
            {
                Context = resolved,
                Source = ContextResolutionSource.Resolver
            };
        }

        return context.ResolutionContext.Boundary == ContextIngressBoundary.External
            ? new ContextResolutionResult<TContext>
            {
                Context = resolved,
                Source = ContextResolutionSource.Resolver
            }
            : new ContextResolutionResult<TContext>
            {
                Context = propagated,
                Source = ContextResolutionSource.Propagated
            };
    }
}
