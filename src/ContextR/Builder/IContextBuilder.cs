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
}
