using Microsoft.Extensions.DependencyInjection.Extensions;

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
        builder.Services.TryAddSingleton<IContextTransportPolicy<TContext>, TPolicy>();
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
        builder.Services.TryAddSingleton<IContextPropagationFailureHandler<TContext>, THandler>();
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

        builder.Services.TryAddSingleton<IContextPropagationFailureHandler<TContext>>(
            _ => new DelegateContextPropagationFailureHandler<TContext>(handler));

        return builder;
    }

    private sealed class DelegateContextPropagationFailureHandler<TContext>(
        Func<PropagationFailureContext, PropagationFailureAction> handler)
        : IContextPropagationFailureHandler<TContext>
        where TContext : class
    {
        public PropagationFailureAction Handle(PropagationFailureContext failure) => handler(failure);
    }
}
