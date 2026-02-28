using System.Linq.Expressions;
using ContextR.Propagation.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContextR.Propagation.Mapping;

/// <summary>
/// Extension methods for configuring property-based context propagation
/// on <see cref="IContextRegistrationBuilder{TContext}"/>.
/// </summary>
public static class ContextRPropagationExtensions
{
    /// <summary>
    /// Configures advanced property mappings through a fluent mapping DSL.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="builder">The context registration builder.</param>
    /// <param name="configure">Mapping configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IContextRegistrationBuilder<TContext> Map<TContext>(
        this IContextRegistrationBuilder<TContext> builder,
        Func<ContextMapBuilder<TContext>, ContextMapBuilder<TContext>> configure)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        _ = configure(new ContextMapBuilder<TContext>(builder));
        return builder;
    }

    /// <summary>
    /// Maps a context property to a transport key name (e.g., an HTTP header).
    /// <para>
    /// Call multiple times to map several properties. The framework auto-generates
    /// an <see cref="IContextPropagator{TContext}"/> from all mapped properties.
    /// </para>
    /// <para>
    /// Mutually exclusive with <c>UsePropagator</c>.
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <typeparam name="TProperty">The property type (must be <see cref="string"/>,
    /// implement <see cref="IParsable{TSelf}"/>, or be convertible via <see cref="Convert"/>).</typeparam>
    /// <param name="builder">The context registration builder.</param>
    /// <param name="property">An expression selecting the property (e.g., <c>c =&gt; c.TenantId</c>).</param>
    /// <param name="key">The transport key name (e.g., <c>"X-Tenant-Id"</c>).</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IContextRegistrationBuilder<TContext> MapProperty<TContext, TProperty>(
        this IContextRegistrationBuilder<TContext> builder,
        Expression<Func<TContext, TProperty>> property,
        string key)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        builder.Services.AddSingleton<IPropertyMapping<TContext>>(
            sp => PropertyMapping.Create(
                property,
                key,
                sp.GetService<IContextPayloadSerializer<TContext>>(),
                sp.GetService<IContextTransportPolicy<TContext>>()));

        builder.Services.TryAddSingleton<IContextPropagator<TContext>>(sp =>
            new MappingContextPropagator<TContext>(
                sp.GetServices<IPropertyMapping<TContext>>(),
                sp,
                sp.GetService<ContextPropagationFailureHandlerRegistry<TContext>>()));

        return builder;
    }
}
