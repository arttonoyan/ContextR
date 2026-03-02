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
    /// Enables nullability-based requirement conventions for mapped properties.
    /// Non-nullable properties are treated as required, nullable properties as optional.
    /// This behavior is enabled by default. Explicit <c>Required()</c>/<c>Optional()</c> calls always take precedence.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UseNullabilityConventions<TContext>(
        this IContextRegistrationBuilder<TContext> builder)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = NullabilityRequirementConventions.GetOrAddOptions<TContext>(builder.Services);
        options.Enabled = true;
        return builder;
    }

    /// <summary>
    /// Disables nullability-based requirement conventions for mapped properties.
    /// When disabled, inferred requirement defaults to optional unless explicitly configured.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> DisableNullabilityConventions<TContext>(
        this IContextRegistrationBuilder<TContext> builder)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = NullabilityRequirementConventions.GetOrAddOptions<TContext>(builder.Services);
        options.Enabled = false;
        return builder;
    }

    /// <summary>
    /// Configures advanced property mappings through a fluent mapping DSL.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="builder">The context registration builder.</param>
    /// <param name="configure">Mapping configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IContextRegistrationBuilder<TContext> Map<TContext>(
        this IContextRegistrationBuilder<TContext> builder,
        Action<ContextMapBuilder<TContext>> configure)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var mapBuilder = new ContextMapBuilder<TContext>(builder);
        configure(mapBuilder);
        mapBuilder.CompletePendingProperties();
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
    /// <param name="oversizeBehaviorOverride">Optional oversize behavior override for this property.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IContextRegistrationBuilder<TContext> MapProperty<TContext, TProperty>(
        this IContextRegistrationBuilder<TContext> builder,
        Expression<Func<TContext, TProperty>> property,
        string key,
        ContextOversizeBehavior? oversizeBehaviorOverride = null)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var inferredRequirement = NullabilityRequirementConventions.IsEnabled<TContext>(builder.Services)
            ? NullabilityRequirementConventions.ResolveRequirement(property)
            : PropertyRequirement.Optional;

        builder.Services.AddSingleton<IPropertyMapping<TContext>>(
            sp =>
            {
                var executionScope = sp.GetRequiredService<IPropagationExecutionScope>();
                var policyRegistry = sp.GetService<ContextPropagationStrategyPolicyRegistry<TContext>>();
                return PropertyMapping.Create(
                    property,
                    key,
                    sp.GetService<IContextPayloadSerializer<TContext>>(),
                    sp.GetService<IContextTransportPolicy<TContext>>(),
                    sp.GetService<IContextPayloadChunkingStrategy<TContext>>(),
                    ctx =>
                    {
                        var policy = policyRegistry?.Resolve(sp, executionScope.CurrentDomain);
                        if (policy is null)
                            return null;

                        var policyContext = new ContextPropagationStrategyPolicyContext
                        {
                            ContextType = ctx.ContextType,
                            Key = ctx.Key,
                            PropertyType = ctx.PropertyType,
                            Direction = ctx.Direction,
                            PayloadSizeBytes = ctx.PayloadSizeBytes,
                            Domain = executionScope.CurrentDomain
                        };

                        return policy.Select(policyContext);
                    },
                    requirement: inferredRequirement,
                    oversizeBehaviorOverride: oversizeBehaviorOverride);
            });

        builder.Services.TryAddSingleton<IPropagationExecutionScope, AsyncLocalPropagationExecutionScope>();
        builder.Services.TryAddSingleton<IContextPropagator<TContext>>(sp =>
            new MappingContextPropagator<TContext>(
                sp.GetServices<IPropertyMapping<TContext>>(),
                sp,
                sp.GetRequiredService<IPropagationExecutionScope>(),
                sp.GetService<ContextPropagationFailureHandlerRegistry<TContext>>()));

        return builder;
    }
}
