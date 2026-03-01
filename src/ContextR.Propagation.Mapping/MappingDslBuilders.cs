using System.Linq.Expressions;
using ContextR.Propagation.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContextR.Propagation.Mapping;

/// <summary>
/// Requirement level for a mapped property.
/// </summary>
public enum PropertyRequirement
{
    /// <summary>
    /// Property is optional. Missing/invalid values are ignored.
    /// </summary>
    Optional = 0,

    /// <summary>
    /// Property is required. Missing/invalid values fail extraction/injection.
    /// </summary>
    Required = 1
}

/// <summary>
/// Fluent builder for advanced map configuration.
/// </summary>
/// <typeparam name="TContext">The context type being configured.</typeparam>
public sealed class ContextMapBuilder<TContext>
    where TContext : class
{
    private readonly IContextRegistrationBuilder<TContext> _registrationBuilder;
    private ContextOversizeBehavior? _defaultOversizeBehavior;

    internal ContextMapBuilder(IContextRegistrationBuilder<TContext> registrationBuilder)
    {
        _registrationBuilder = registrationBuilder;
    }

    /// <summary>
    /// Starts configuration for a single mapped property.
    /// </summary>
    public ContextMapPropertyBuilder<TContext, TProperty> Property<TProperty>(
        Expression<Func<TContext, TProperty>> property,
        string key)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new ContextMapPropertyBuilder<TContext, TProperty>(this, property, key);
    }

    /// <summary>
    /// Sets context-level default oversize behavior for mapped properties.
    /// </summary>
    public ContextMapBuilder<TContext> DefaultOversizeBehavior(ContextOversizeBehavior behavior)
    {
        _defaultOversizeBehavior = behavior;
        return this;
    }

    internal ContextMapBuilder<TContext> AddProperty<TProperty>(
        Expression<Func<TContext, TProperty>> property,
        string key,
        PropertyRequirement requirement,
        ContextOversizeBehavior? oversizeBehaviorOverride)
    {
        _registrationBuilder.Services.AddSingleton<IPropertyMapping<TContext>>(
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
                    requirement,
                    oversizeBehaviorOverride ?? _defaultOversizeBehavior);
            });

        _registrationBuilder.Services.TryAddSingleton<IPropagationExecutionScope, AsyncLocalPropagationExecutionScope>();
        _registrationBuilder.Services.TryAddSingleton<IContextPropagator<TContext>>(sp =>
            new MappingContextPropagator<TContext>(
                sp.GetServices<IPropertyMapping<TContext>>(),
                sp,
                sp.GetRequiredService<IPropagationExecutionScope>(),
                sp.GetService<ContextPropagationFailureHandlerRegistry<TContext>>()));

        return this;
    }
}

/// <summary>
/// Fluent builder for per-property map options.
/// </summary>
/// <typeparam name="TContext">The context type being configured.</typeparam>
/// <typeparam name="TProperty">The mapped property type.</typeparam>
public sealed class ContextMapPropertyBuilder<TContext, TProperty>
    where TContext : class
{
    private readonly ContextMapBuilder<TContext> _parent;
    private readonly Expression<Func<TContext, TProperty>> _property;
    private readonly string _key;
    private ContextOversizeBehavior? _oversizeBehaviorOverride;

    internal ContextMapPropertyBuilder(
        ContextMapBuilder<TContext> parent,
        Expression<Func<TContext, TProperty>> property,
        string key)
    {
        _parent = parent;
        _property = property;
        _key = key;
    }

    /// <summary>
    /// Marks this mapped property as required.
    /// </summary>
    public ContextMapBuilder<TContext> Required()
    {
        return _parent.AddProperty(_property, _key, PropertyRequirement.Required, _oversizeBehaviorOverride);
    }

    /// <summary>
    /// Marks this mapped property as optional.
    /// </summary>
    public ContextMapBuilder<TContext> Optional()
    {
        return _parent.AddProperty(_property, _key, PropertyRequirement.Optional, _oversizeBehaviorOverride);
    }

    /// <summary>
    /// Overrides oversize behavior for this property.
    /// </summary>
    public ContextMapPropertyBuilder<TContext, TProperty> OversizeBehavior(ContextOversizeBehavior behavior)
    {
        _oversizeBehaviorOverride = behavior;
        return this;
    }
}
