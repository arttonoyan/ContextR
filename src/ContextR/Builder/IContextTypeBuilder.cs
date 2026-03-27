using Microsoft.Extensions.DependencyInjection;

namespace ContextR;

/// <summary>
/// Provides a context-type-specific fluent configuration surface.
/// Transport packages extend this interface with extension methods
/// (e.g., <c>.UseAspNetCore()</c>, <c>.UseGlobalHttpPropagation()</c>, <c>.MapProperty()</c>).
/// </summary>
/// <typeparam name="TContext">The context type being configured.</typeparam>
public interface IContextTypeBuilder<TContext> where TContext : class
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
}
