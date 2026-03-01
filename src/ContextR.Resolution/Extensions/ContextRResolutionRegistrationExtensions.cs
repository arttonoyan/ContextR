using System.Linq;
using ContextR.Resolution.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Resolution;

/// <summary>
/// Extension methods for configuring context resolution behavior.
/// </summary>
public static class ContextRResolutionRegistrationExtensions
{
    /// <summary>
    /// Registers a typed resolver implementation.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UseResolver<TContext, TResolver>(
        this IContextRegistrationBuilder<TContext> builder)
        where TContext : class
        where TResolver : class, IContextResolver<TContext>
    {
        ArgumentNullException.ThrowIfNull(builder);
        EnsureResolutionCore(builder.Services);

        builder.Services.AddSingleton<TResolver>();
        var registry = GetOrAddResolverRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, sp => sp.GetRequiredService<TResolver>());
        return builder;
    }

    /// <summary>
    /// Registers a resolver delegate.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UseResolver<TContext>(
        this IContextRegistrationBuilder<TContext> builder,
        Func<ContextResolutionContext, TContext?> resolver)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(resolver);
        EnsureResolutionCore(builder.Services);

        var registry = GetOrAddResolverRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, _ => new DelegateContextResolver<TContext>(resolver));
        return builder;
    }

    /// <summary>
    /// Registers a resolver factory resolved from DI.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UseResolver<TContext>(
        this IContextRegistrationBuilder<TContext> builder,
        Func<IServiceProvider, IContextResolver<TContext>> factory)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(factory);
        EnsureResolutionCore(builder.Services);

        var registry = GetOrAddResolverRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, factory);
        return builder;
    }

    /// <summary>
    /// Registers a typed resolution policy implementation.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UseResolutionPolicy<TContext, TPolicy>(
        this IContextRegistrationBuilder<TContext> builder)
        where TContext : class
        where TPolicy : class, IContextResolutionPolicy<TContext>
    {
        ArgumentNullException.ThrowIfNull(builder);
        EnsureResolutionCore(builder.Services);

        builder.Services.AddSingleton<TPolicy>();
        var registry = GetOrAddPolicyRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, sp => sp.GetRequiredService<TPolicy>());
        return builder;
    }

    /// <summary>
    /// Registers a resolution policy delegate.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UseResolutionPolicy<TContext>(
        this IContextRegistrationBuilder<TContext> builder,
        Func<ContextResolutionPolicyContext<TContext>, ContextResolutionResult<TContext>> policy)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(policy);
        EnsureResolutionCore(builder.Services);

        var registry = GetOrAddPolicyRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, _ => new DelegateContextResolutionPolicy<TContext>(policy));
        return builder;
    }

    /// <summary>
    /// Registers a resolution policy factory resolved from DI.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UseResolutionPolicy<TContext>(
        this IContextRegistrationBuilder<TContext> builder,
        Func<IServiceProvider, IContextResolutionPolicy<TContext>> factory)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(factory);
        EnsureResolutionCore(builder.Services);

        var registry = GetOrAddPolicyRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, factory);
        return builder;
    }

    private static void EnsureResolutionCore(IServiceCollection services)
    {
        services.AddContextRResolution();
    }

    private static ContextResolverRegistry<TContext> GetOrAddResolverRegistry<TContext>(IServiceCollection services)
        where TContext : class
    {
        var existing = services
            .FirstOrDefault(d => d.ServiceType == typeof(ContextResolverRegistry<TContext>))
            ?.ImplementationInstance as ContextResolverRegistry<TContext>;

        if (existing is not null)
            return existing;

        var created = new ContextResolverRegistry<TContext>();
        services.AddSingleton(created);
        return created;
    }

    private static ContextResolutionPolicyRegistry<TContext> GetOrAddPolicyRegistry<TContext>(IServiceCollection services)
        where TContext : class
    {
        var existing = services
            .FirstOrDefault(d => d.ServiceType == typeof(ContextResolutionPolicyRegistry<TContext>))
            ?.ImplementationInstance as ContextResolutionPolicyRegistry<TContext>;

        if (existing is not null)
            return existing;

        var created = new ContextResolutionPolicyRegistry<TContext>();
        services.AddSingleton(created);
        return created;
    }

    private sealed class DelegateContextResolver<TContext>(
        Func<ContextResolutionContext, TContext?> resolver)
        : IContextResolver<TContext>
        where TContext : class
    {
        public TContext? Resolve(ContextResolutionContext context) => resolver(context);
    }

    private sealed class DelegateContextResolutionPolicy<TContext>(
        Func<ContextResolutionPolicyContext<TContext>, ContextResolutionResult<TContext>> policy)
        : IContextResolutionPolicy<TContext>
        where TContext : class
    {
        public ContextResolutionResult<TContext> Resolve(ContextResolutionPolicyContext<TContext> context) => policy(context);
    }
}
