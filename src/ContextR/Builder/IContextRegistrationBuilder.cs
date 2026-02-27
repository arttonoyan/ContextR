namespace ContextR;

/// <summary>
/// Provides a context-type-specific fluent configuration surface.
/// </summary>
/// <typeparam name="TContext">The context type being configured.</typeparam>
public interface IContextRegistrationBuilder<TContext> where TContext : class
{
}
