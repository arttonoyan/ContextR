namespace ContextR;

/// <summary>
/// Configures ContextR context registrations.
/// </summary>
public interface IContextRBuilder
{
    /// <summary>
    /// Registers a context type for ContextR and optionally configures its builder.
    /// </summary>
    /// <typeparam name="TContext">The context type to register.</typeparam>
    /// <param name="configure">Optional context-specific configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    IContextRBuilder Add<TContext>(Action<IContextBuilder<TContext>>? configure = null)
        where TContext : class;
}
