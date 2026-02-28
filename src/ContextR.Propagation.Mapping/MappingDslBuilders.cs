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

    internal ContextMapBuilder<TContext> AddProperty<TProperty>(
        Expression<Func<TContext, TProperty>> property,
        string key,
        PropertyRequirement requirement)
    {
        _registrationBuilder.Services.AddSingleton<IPropertyMapping<TContext>>(
            sp => PropertyMapping.Create(
                property,
                key,
                sp.GetService<IContextPayloadSerializer<TContext>>(),
                sp.GetService<IContextTransportPolicy<TContext>>(),
                requirement));

        _registrationBuilder.Services.TryAddSingleton<IContextPropagator<TContext>>(sp =>
            new MappingContextPropagator<TContext>(
                sp.GetServices<IPropertyMapping<TContext>>(),
                sp,
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
        return _parent.AddProperty(_property, _key, PropertyRequirement.Required);
    }

    /// <summary>
    /// Marks this mapped property as optional.
    /// </summary>
    public ContextMapBuilder<TContext> Optional()
    {
        return _parent.AddProperty(_property, _key, PropertyRequirement.Optional);
    }
}
