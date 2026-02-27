using System.Linq.Expressions;
using ContextR.Propagation.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContextR;

/// <summary>
/// Extension methods for configuring property-based context propagation
/// on <see cref="IContextRegistrationBuilder{TContext}"/>.
/// </summary>
public static class ContextRPropagationExtensions
{
    /// <summary>
    /// Maps a context property to a transport key name (e.g., an HTTP header).
    /// <para>
    /// Call multiple times to map several properties. The framework auto-generates
    /// an <see cref="IContextPropagator{TContext}"/> from all mapped properties.
    /// </para>
    /// <para>
    /// Mutually exclusive with <see cref="IContextRegistrationBuilder{TContext}.UsePropagator{TPropagator}"/>.
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
        ArgumentNullException.ThrowIfNull(property);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        builder.Services.AddSingleton<IPropertyMapping<TContext>>(
            _ => PropertyMapping.Create(property, key));

        builder.Services.TryAddSingleton<IContextPropagator<TContext>>(sp =>
            new MappingContextPropagator<TContext>(sp.GetServices<IPropertyMapping<TContext>>()));

        return builder;
    }
}
