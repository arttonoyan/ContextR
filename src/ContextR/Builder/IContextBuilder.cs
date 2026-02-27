namespace ContextR;

/// <summary>
/// Configures ContextR context registrations.
/// </summary>
public interface IContextBuilder
{
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
}
