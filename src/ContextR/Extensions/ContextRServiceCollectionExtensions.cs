using ContextR.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContextR;

/// <summary>
/// Extension methods for registering ContextR core services.
/// </summary>
public static class ContextRServiceCollectionExtensions
{
    /// <summary>
    /// Registers ContextR core services and applies context registrations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The fluent ContextR configuration callback.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddContextR(
        this IServiceCollection services,
        Action<IContextBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ContextBuilder();
        configure(builder);

        services.TryAddSingleton<MutableContextAccessor>();
        services.TryAddSingleton<IContextAccessor>(static sp => sp.GetRequiredService<MutableContextAccessor>());
        services.TryAddSingleton<IContextWriter>(static sp => sp.GetRequiredService<MutableContextAccessor>());
        services.TryAddScoped<IContextSnapshot>(static _ => new ContextSnapshot(MutableContextAccessor.CaptureCurrentValues()));

        return services;
    }
}
