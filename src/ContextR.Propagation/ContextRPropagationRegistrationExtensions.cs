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
}
