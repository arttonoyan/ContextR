namespace ContextR.Resolution;

/// <summary>
/// Extension methods for configuring context resolution behavior.
/// </summary>
public static class ContextRResolutionRegistrationExtensions
{
    /// <summary>
    /// Opens a resolution-specific fluent scope for this context registration.
    /// </summary>
    public static IContextTypeBuilder<TContext> AddResolution<TContext>(
        this IContextTypeBuilder<TContext> builder,
        Action<IResolutionBuilder<TContext>> configure)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddContextRResolution();
        configure(new ResolutionBuilder<TContext>(builder));
        return builder;
    }

    /// <summary>
    /// Registers a typed resolver implementation.
    /// </summary>
    public static IContextTypeBuilder<TContext> UseResolver<TContext, TResolver>(
        this IContextTypeBuilder<TContext> builder)
        where TContext : class
        where TResolver : class, IContextResolver<TContext>
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddResolution(r => r.UseResolver<TResolver>());
    }

    /// <summary>
    /// Registers a resolver delegate.
    /// </summary>
    public static IContextTypeBuilder<TContext> UseResolver<TContext>(
        this IContextTypeBuilder<TContext> builder,
        Func<ContextResolutionContext, TContext?> resolver)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(resolver);
        return builder.AddResolution(r => r.UseResolver(resolver));
    }

    /// <summary>
    /// Registers a resolver factory resolved from DI.
    /// </summary>
    public static IContextTypeBuilder<TContext> UseResolver<TContext>(
        this IContextTypeBuilder<TContext> builder,
        Func<IServiceProvider, IContextResolver<TContext>> factory)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(factory);
        return builder.AddResolution(r => r.UseResolver(factory));
    }

    /// <summary>
    /// Registers a typed resolution policy implementation.
    /// </summary>
    public static IContextTypeBuilder<TContext> UseResolutionPolicy<TContext, TPolicy>(
        this IContextTypeBuilder<TContext> builder)
        where TContext : class
        where TPolicy : class, IContextResolutionPolicy<TContext>
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddResolution(r => r.UseResolutionPolicy<TPolicy>());
    }

    /// <summary>
    /// Registers a resolution policy delegate.
    /// </summary>
    public static IContextTypeBuilder<TContext> UseResolutionPolicy<TContext>(
        this IContextTypeBuilder<TContext> builder,
        Func<ContextResolutionPolicyContext<TContext>, ContextResolutionResult<TContext>> policy)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(policy);
        return builder.AddResolution(r => r.UseResolutionPolicy(policy));
    }

    /// <summary>
    /// Registers a resolution policy factory resolved from DI.
    /// </summary>
    public static IContextTypeBuilder<TContext> UseResolutionPolicy<TContext>(
        this IContextTypeBuilder<TContext> builder,
        Func<IServiceProvider, IContextResolutionPolicy<TContext>> factory)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(factory);
        return builder.AddResolution(r => r.UseResolutionPolicy(factory));
    }
}
