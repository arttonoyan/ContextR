using ContextR.Resolution.Internal;

namespace ContextR.Resolution;

internal sealed class ResolutionBuilder<TContext>(IContextTypeBuilder<TContext> builder)
    : IResolutionBuilder<TContext>
    where TContext : class
{
    public IResolutionBuilder<TContext> UseResolver<TResolver>()
        where TResolver : class, IContextResolver<TContext>
    {
        ResolutionRegistrationHelpers.RegisterResolver<TContext, TResolver>(builder);
        return this;
    }

    public IResolutionBuilder<TContext> UseResolver(Func<ContextResolutionContext, TContext?> resolver)
    {
        ResolutionRegistrationHelpers.RegisterResolver(builder, resolver);
        return this;
    }

    public IResolutionBuilder<TContext> UseResolver(Func<IServiceProvider, IContextResolver<TContext>> factory)
    {
        ResolutionRegistrationHelpers.RegisterResolver(builder, factory);
        return this;
    }

    public IResolutionBuilder<TContext> UseResolutionPolicy<TPolicy>()
        where TPolicy : class, IContextResolutionPolicy<TContext>
    {
        ResolutionRegistrationHelpers.RegisterResolutionPolicy<TContext, TPolicy>(builder);
        return this;
    }

    public IResolutionBuilder<TContext> UseResolutionPolicy(
        Func<ContextResolutionPolicyContext<TContext>, ContextResolutionResult<TContext>> policy)
    {
        ResolutionRegistrationHelpers.RegisterResolutionPolicy(builder, policy);
        return this;
    }

    public IResolutionBuilder<TContext> UseResolutionPolicy(
        Func<IServiceProvider, IContextResolutionPolicy<TContext>> factory)
    {
        ResolutionRegistrationHelpers.RegisterResolutionPolicy(builder, factory);
        return this;
    }
}
