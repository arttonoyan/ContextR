using System.Linq.Expressions;
using ContextR.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContextR;

internal sealed class ContextRegistrationBuilder<TContext> : IContextRegistrationBuilder<TContext>
    where TContext : class
{
    private readonly List<IPropertyMapping<TContext>> _mappings = [];
    private bool _hasPropagator;

    public ContextRegistrationBuilder(IServiceCollection services, string? domain)
    {
        Services = services;
        Domain = domain;
    }

    public IServiceCollection Services { get; }

    public string? Domain { get; }

    public IContextRegistrationBuilder<TContext> UsePropagator<TPropagator>()
        where TPropagator : class, IContextPropagator<TContext>
    {
        if (_mappings.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot call {nameof(UsePropagator)} after {nameof(MapProperty)} has been used. " +
                $"Use either {nameof(UsePropagator)} or {nameof(MapProperty)}, not both.");
        }

        _hasPropagator = true;
        Services.TryAddSingleton<IContextPropagator<TContext>, TPropagator>();
        return this;
    }

    public IContextRegistrationBuilder<TContext> MapProperty<TProperty>(
        Expression<Func<TContext, TProperty>> property,
        string key)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_hasPropagator)
        {
            throw new InvalidOperationException(
                $"Cannot call {nameof(MapProperty)} after {nameof(UsePropagator)} has been used. " +
                $"Use either {nameof(UsePropagator)} or {nameof(MapProperty)}, not both.");
        }

        _mappings.Add(PropertyMapping.Create(property, key));
        return this;
    }

    internal void Build()
    {
        if (_mappings.Count > 0)
        {
            var mappings = _mappings.ToArray();
            Services.TryAddSingleton<IContextPropagator<TContext>>(
                _ => new MappingContextPropagator<TContext>(mappings));
        }
    }
}
