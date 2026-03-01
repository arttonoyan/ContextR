using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace ContextR.Propagation;

/// <summary>
/// Extension methods for configuring runtime propagation behavior.
/// </summary>
public static class ContextRPropagationRegistrationExtensions
{
    /// <summary>
    /// Registers a custom <see cref="IContextPropagator{TContext}"/> implementation.
    /// </summary>
    /// <typeparam name="TContext">The context type being configured.</typeparam>
    /// <typeparam name="TPropagator">The propagator implementation type.</typeparam>
    /// <param name="builder">The context registration builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IContextRegistrationBuilder<TContext> UsePropagator<TContext, TPropagator>(
        this IContextRegistrationBuilder<TContext> builder)
        where TContext : class
        where TPropagator : class, IContextPropagator<TContext>
    {
        ArgumentNullException.ThrowIfNull(builder);
        EnsureExecutionScopeRegistration(builder.Services);
        builder.Services.TryAddSingleton<IContextPropagator<TContext>, TPropagator>();
        return builder;
    }

    /// <summary>
    /// Registers a payload serializer strategy for mapped properties of <typeparamref name="TContext"/>.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UsePayloadSerializer<TContext, TSerializer>(
        this IContextRegistrationBuilder<TContext> builder)
        where TContext : class
        where TSerializer : class, IContextPayloadSerializer<TContext>
    {
        ArgumentNullException.ThrowIfNull(builder);
        EnsureExecutionScopeRegistration(builder.Services);
        builder.Services.TryAddSingleton<IContextPayloadSerializer<TContext>, TSerializer>();
        return builder;
    }

    /// <summary>
    /// Registers a transport policy strategy for mapped properties of <typeparamref name="TContext"/>.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UseTransportPolicy<TContext, TPolicy>(
        this IContextRegistrationBuilder<TContext> builder)
        where TContext : class
        where TPolicy : class, IContextTransportPolicy<TContext>
    {
        ArgumentNullException.ThrowIfNull(builder);
        EnsureExecutionScopeRegistration(builder.Services);
        builder.Services.TryAddSingleton<IContextTransportPolicy<TContext>, TPolicy>();
        return builder;
    }

    /// <summary>
    /// Registers a runtime strategy policy implementation.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UseStrategyPolicy<TContext, TPolicy>(
        this IContextRegistrationBuilder<TContext> builder)
        where TContext : class
        where TPolicy : class, IContextPropagationStrategyPolicy<TContext>
    {
        ArgumentNullException.ThrowIfNull(builder);
        EnsureExecutionScopeRegistration(builder.Services);
        builder.Services.TryAddSingleton<TPolicy>();

        var registry = GetOrAddStrategyPolicyRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, sp => sp.GetRequiredService<TPolicy>());
        return builder;
    }

    /// <summary>
    /// Registers a runtime strategy policy delegate.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UseStrategyPolicy<TContext>(
        this IContextRegistrationBuilder<TContext> builder,
        Func<ContextPropagationStrategyPolicyContext, ContextOversizeBehavior> policy)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(policy);
        EnsureExecutionScopeRegistration(builder.Services);

        var registry = GetOrAddStrategyPolicyRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, _ => new DelegateContextPropagationStrategyPolicy<TContext>(policy));
        return builder;
    }

    /// <summary>
    /// Registers a runtime strategy policy delegate factory resolved from DI.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UseStrategyPolicy<TContext>(
        this IContextRegistrationBuilder<TContext> builder,
        Func<IServiceProvider, Func<ContextPropagationStrategyPolicyContext, ContextOversizeBehavior>> factory)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(factory);
        EnsureExecutionScopeRegistration(builder.Services);

        var registry = GetOrAddStrategyPolicyRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, sp => new DelegateContextPropagationStrategyPolicy<TContext>(factory(sp)));
        return builder;
    }

    /// <summary>
    /// Registers a propagation failure handler implementation.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> OnPropagationFailure<TContext, THandler>(
        this IContextRegistrationBuilder<TContext> builder)
        where TContext : class
        where THandler : class, IContextPropagationFailureHandler<TContext>
    {
        ArgumentNullException.ThrowIfNull(builder);
        EnsureExecutionScopeRegistration(builder.Services);
        builder.Services.TryAddSingleton<THandler>();

        var registry = GetOrAddFailureRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, sp => sp.GetRequiredService<THandler>());
        return builder;
    }

    /// <summary>
    /// Registers a propagation failure handler delegate.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> OnPropagationFailure<TContext>(
        this IContextRegistrationBuilder<TContext> builder,
        Func<PropagationFailureContext, PropagationFailureAction> handler)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(handler);
        EnsureExecutionScopeRegistration(builder.Services);

        var registry = GetOrAddFailureRegistry<TContext>(builder.Services);
        registry.TryAdd(builder.Domain, _ => new DelegateContextPropagationFailureHandler<TContext>(handler));

        return builder;
    }

    private static void EnsureExecutionScopeRegistration(IServiceCollection services)
    {
        services.TryAddSingleton<IPropagationExecutionScope, AsyncLocalPropagationExecutionScope>();
    }

    private static ContextPropagationFailureHandlerRegistry<TContext> GetOrAddFailureRegistry<TContext>(
        IServiceCollection services)
        where TContext : class
    {
        var existing = services
            .FirstOrDefault(d => d.ServiceType == typeof(ContextPropagationFailureHandlerRegistry<TContext>))
            ?.ImplementationInstance as ContextPropagationFailureHandlerRegistry<TContext>;

        if (existing is not null)
            return existing;

        var created = new ContextPropagationFailureHandlerRegistry<TContext>();
        services.AddSingleton(created);
        return created;
    }

    private static ContextPropagationStrategyPolicyRegistry<TContext> GetOrAddStrategyPolicyRegistry<TContext>(
        IServiceCollection services)
        where TContext : class
    {
        var existing = services
            .FirstOrDefault(d => d.ServiceType == typeof(ContextPropagationStrategyPolicyRegistry<TContext>))
            ?.ImplementationInstance as ContextPropagationStrategyPolicyRegistry<TContext>;

        if (existing is not null)
            return existing;

        var created = new ContextPropagationStrategyPolicyRegistry<TContext>();
        services.AddSingleton(created);
        return created;
    }

    private sealed class DelegateContextPropagationFailureHandler<TContext>(
        Func<PropagationFailureContext, PropagationFailureAction> handler)
        : IContextPropagationFailureHandler<TContext>
        where TContext : class
    {
        public PropagationFailureAction Handle(PropagationFailureContext failure) => handler(failure);
    }

    private sealed class DelegateContextPropagationStrategyPolicy<TContext>(
        Func<ContextPropagationStrategyPolicyContext, ContextOversizeBehavior> policy)
        : IContextPropagationStrategyPolicy<TContext>
        where TContext : class
    {
        public ContextOversizeBehavior Select(ContextPropagationStrategyPolicyContext context) => policy(context);
    }
}
