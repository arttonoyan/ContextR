using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR;

/// <summary>
/// Provides a context-type-specific fluent configuration surface.
/// Transport packages extend this interface with extension methods
/// (e.g., <c>.UseAspNetCore()</c>, <c>.UseGlobalHttpPropagation()</c>).
/// </summary>
/// <typeparam name="TContext">The context type being configured.</typeparam>
public interface IContextRegistrationBuilder<TContext> where TContext : class
{
    /// <summary>
    /// Gets the service collection for registering transport-specific services.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Gets the domain this context type is registered under,
    /// or <see langword="null"/> for the default (domainless) registration.
    /// </summary>
    string? Domain { get; }

    /// <summary>
    /// Registers a custom <see cref="IContextPropagator{TContext}"/> implementation.
    /// Mutually exclusive with <see cref="MapProperty{TProperty}"/>.
    /// </summary>
    /// <typeparam name="TPropagator">The propagator implementation type.</typeparam>
    /// <returns>The same builder for fluent chaining.</returns>
    IContextRegistrationBuilder<TContext> UsePropagator<TPropagator>()
        where TPropagator : class, IContextPropagator<TContext>;

    /// <summary>
    /// Maps a context property to a key name for transport propagation.
    /// The framework auto-generates an <see cref="IContextPropagator{TContext}"/>
    /// from all mapped properties.
    /// Mutually exclusive with <see cref="UsePropagator{TPropagator}"/>.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="property">An expression selecting the property (e.g., <c>c => c.TenantId</c>).</param>
    /// <param name="key">The transport key name (e.g., header name <c>"X-Tenant-Id"</c>).</param>
    /// <returns>The same builder for fluent chaining.</returns>
    IContextRegistrationBuilder<TContext> MapProperty<TProperty>(
        Expression<Func<TContext, TProperty>> property,
        string key);
}
