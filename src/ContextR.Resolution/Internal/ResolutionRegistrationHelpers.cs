using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Resolution.Internal;

internal static class ResolutionRegistrationHelpers
{
    internal static void RegisterResolver<TContext, TResolver>(IContextRegistrationBuilder<TContext> builder)
        where TContext : class
        where TResolver : class, IContextResolver<TContext>
    {
        builder.Services.AddSingleton<TResolver>();
        var registry = GetOrAddResolverRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, sp => sp.GetRequiredService<TResolver>());
    }

    internal static void RegisterResolver<TContext>(
        IContextRegistrationBuilder<TContext> builder,
        Func<ContextResolutionContext, TContext?> resolver)
        where TContext : class
    {
        var registry = GetOrAddResolverRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, _ => new DelegateContextResolver<TContext>(resolver));
    }

    internal static void RegisterResolver<TContext>(
        IContextRegistrationBuilder<TContext> builder,
        Func<IServiceProvider, IContextResolver<TContext>> factory)
        where TContext : class
    {
        var registry = GetOrAddResolverRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, factory);
    }

    internal static void RegisterResolutionPolicy<TContext, TPolicy>(IContextRegistrationBuilder<TContext> builder)
        where TContext : class
        where TPolicy : class, IContextResolutionPolicy<TContext>
    {
        builder.Services.AddSingleton<TPolicy>();
        var registry = GetOrAddPolicyRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, sp => sp.GetRequiredService<TPolicy>());
    }

    internal static void RegisterResolutionPolicy<TContext>(
        IContextRegistrationBuilder<TContext> builder,
        Func<ContextResolutionPolicyContext<TContext>, ContextResolutionResult<TContext>> policy)
        where TContext : class
    {
        var registry = GetOrAddPolicyRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, _ => new DelegateContextResolutionPolicy<TContext>(policy));
    }

    internal static void RegisterResolutionPolicy<TContext>(
        IContextRegistrationBuilder<TContext> builder,
        Func<IServiceProvider, IContextResolutionPolicy<TContext>> factory)
        where TContext : class
    {
        var registry = GetOrAddPolicyRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, factory);
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
