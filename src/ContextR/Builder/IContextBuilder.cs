using Microsoft.Extensions.DependencyInjection;

namespace ContextR;

/// <summary>
/// Configures ContextR context registrations.
/// </summary>
public interface IContextBuilder
{
    /// <summary>
    /// Gets the service collection that ContextR services are registered into.
    /// Transport packages use this to register their own services via extension methods.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Registers a context type for ContextR and optionally configures its registration builder.
    /// </summary>
    /// <typeparam name="TContext">The context type to register.</typeparam>
    /// <param name="configure">Optional context-specific configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    IContextBuilder Add<TContext>(Action<IContextRegistrationBuilder<TContext>>? configure = null)
        where TContext : class;

    /// <summary>
    /// Registers context types within a specific domain.
    /// </summary>
    /// <param name="domain">The domain identifier.</param>
    /// <param name="configure">A callback to configure context registrations for this domain.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    IContextBuilder AddDomain(string domain, Action<IDomainContextBuilder> configure);

    /// <summary>
    /// Configures the domain policy that controls how parameterless context operations
    /// resolve to a specific domain.
    /// </summary>
    /// <param name="configure">A callback to configure the <see cref="ContextDomainPolicy"/>.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    IContextBuilder AddDomainPolicy(Action<ContextDomainPolicy> configure);

    /// <summary>
    /// Configures the default domain selector used by parameterless context operations.
    /// </summary>
    /// <param name="defaultDomainSelector">
    /// A runtime selector that returns the default domain for the current request scope.
    /// </param>
    /// <returns>The same builder for fluent chaining.</returns>
    IContextBuilder AddDomainPolicy(Func<IServiceProvider, string?> defaultDomainSelector);
}
