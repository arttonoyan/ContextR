namespace ContextR.Resolution;

/// <summary>
/// Fluent builder for resolution-specific configuration under a context registration.
/// </summary>
/// <typeparam name="TContext">The context type being configured.</typeparam>
public interface IResolutionBuilder<TContext>
    where TContext : class
{
    /// <summary>
    /// Registers a typed resolver implementation.
    /// </summary>
    IResolutionBuilder<TContext> UseResolver<TResolver>()
        where TResolver : class, IContextResolver<TContext>;

    /// <summary>
    /// Registers a resolver delegate.
    /// </summary>
    IResolutionBuilder<TContext> UseResolver(Func<ContextResolutionContext, TContext?> resolver);

    /// <summary>
    /// Registers a resolver factory resolved from DI.
    /// </summary>
    IResolutionBuilder<TContext> UseResolver(Func<IServiceProvider, IContextResolver<TContext>> factory);

    /// <summary>
    /// Registers a typed resolution policy implementation.
    /// </summary>
    IResolutionBuilder<TContext> UseResolutionPolicy<TPolicy>()
        where TPolicy : class, IContextResolutionPolicy<TContext>;

    /// <summary>
    /// Registers a resolution policy delegate.
    /// </summary>
    IResolutionBuilder<TContext> UseResolutionPolicy(
        Func<ContextResolutionPolicyContext<TContext>, ContextResolutionResult<TContext>> policy);

    /// <summary>
    /// Registers a resolution policy factory resolved from DI.
    /// </summary>
    IResolutionBuilder<TContext> UseResolutionPolicy(
        Func<IServiceProvider, IContextResolutionPolicy<TContext>> factory);
}
