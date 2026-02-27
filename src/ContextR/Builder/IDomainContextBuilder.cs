namespace ContextR;

/// <summary>
/// Configures context registrations within a specific domain.
/// </summary>
public interface IDomainContextBuilder
{
    /// <summary>
    /// Registers a context type within this domain and optionally configures its registration builder.
    /// </summary>
    /// <typeparam name="TContext">The context type to register.</typeparam>
    /// <param name="configure">Optional context-specific configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    IDomainContextBuilder Add<TContext>(Action<IContextRegistrationBuilder<TContext>>? configure = null)
        where TContext : class;
}
