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

internal interface IMapPendingProperty
{
    void FinalizeWithDefault();
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
    private bool _applyConventionByDefault;
    private IMapPendingProperty? _pendingProperty;

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

        _pendingProperty?.FinalizeWithDefault();

        var created = new ContextMapPropertyBuilder<TContext, TProperty>(
            this,
            property,
            key,
            _applyConventionByDefault);
        _pendingProperty = created;
        return created;
    }

    /// <summary>
    /// Sets context-level default oversize behavior for mapped properties.
    /// </summary>
    public ContextMapBuilder<TContext> DefaultOversizeBehavior(ContextOversizeBehavior behavior)
    {
        _defaultOversizeBehavior = behavior;
        return this;
    }

    /// <summary>
    /// Applies nullability convention as the default requirement mode
    /// for all properties configured after this call.
    /// Explicit per-property <c>Required()</c>/<c>Optional()</c> still win.
    /// </summary>
    public ContextMapBuilder<TContext> ByConvention()
    {
        _applyConventionByDefault = true;
        return this;
    }

    internal ContextMapBuilder<TContext> AddProperty<TProperty>(
        Expression<Func<TContext, TProperty>> property,
        string key,
        PropertyRequirement? requirement,
        ContextOversizeBehavior? oversizeBehaviorOverride)
    {
        var effectiveRequirement = requirement;
        if (effectiveRequirement is null)
        {
            effectiveRequirement = NullabilityRequirementConventions.IsEnabled<TContext>(_registrationBuilder.Services)
                ? NullabilityRequirementConventions.ResolveRequirement(property)
                : PropertyRequirement.Optional;
        }

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
                    effectiveRequirement.Value,
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

    internal void CompletePendingProperties()
    {
        _pendingProperty?.FinalizeWithDefault();
    }

    internal void ClearPending()
    {
        _pendingProperty = null;
    }
}

/// <summary>
/// Fluent builder for per-property map options.
/// </summary>
/// <typeparam name="TContext">The context type being configured.</typeparam>
/// <typeparam name="TProperty">The mapped property type.</typeparam>
public sealed class ContextMapPropertyBuilder<TContext, TProperty>
    : IMapPendingProperty
    where TContext : class
{
    private readonly ContextMapBuilder<TContext> _parent;
    private readonly Expression<Func<TContext, TProperty>> _property;
    private readonly string _key;
    private readonly bool _applyConventionByDefault;
    private ContextOversizeBehavior? _oversizeBehaviorOverride;

    internal ContextMapPropertyBuilder(
        ContextMapBuilder<TContext> parent,
        Expression<Func<TContext, TProperty>> property,
        string key,
        bool applyConventionByDefault)
    {
        _parent = parent;
        _property = property;
        _key = key;
        _applyConventionByDefault = applyConventionByDefault;
    }

    /// <summary>
    /// Marks this mapped property as required.
    /// </summary>
    public ContextMapBuilder<TContext> Required()
    {
        var result = _parent.AddProperty(_property, _key, PropertyRequirement.Required, _oversizeBehaviorOverride);
        _parent.ClearPending();
        return result;
    }

    /// <summary>
    /// Marks this mapped property as optional.
    /// </summary>
    public ContextMapBuilder<TContext> Optional()
    {
        var result = _parent.AddProperty(_property, _key, PropertyRequirement.Optional, _oversizeBehaviorOverride);
        _parent.ClearPending();
        return result;
    }

    /// <summary>
    /// Applies requirement by nullability convention (non-nullable => required, nullable => optional).
    /// Conventions are enabled by default and can be disabled via <c>DisableNullabilityConventions()</c>.
    /// </summary>
    public ContextMapBuilder<TContext> ByConvention()
    {
        var result = _parent.AddProperty(_property, _key, null, _oversizeBehaviorOverride);
        _parent.ClearPending();
        return result;
    }

    /// <summary>
    /// Starts configuration for the next mapped property.
    /// The current property is finalized using map-level defaults first.
    /// </summary>
    public ContextMapPropertyBuilder<TContext, TNextProperty> Property<TNextProperty>(
        Expression<Func<TContext, TNextProperty>> property,
        string key)
    {
        ((IMapPendingProperty)this).FinalizeWithDefault();
        return _parent.Property(property, key);
    }

    /// <summary>
    /// Overrides oversize behavior for this property.
    /// </summary>
    public ContextMapPropertyBuilder<TContext, TProperty> OversizeBehavior(ContextOversizeBehavior behavior)
    {
        _oversizeBehaviorOverride = behavior;
        return this;
    }

    void IMapPendingProperty.FinalizeWithDefault()
    {
        _parent.AddProperty(
            _property,
            _key,
            _applyConventionByDefault ? null : PropertyRequirement.Optional,
            _oversizeBehaviorOverride);
        _parent.ClearPending();
    }
}
