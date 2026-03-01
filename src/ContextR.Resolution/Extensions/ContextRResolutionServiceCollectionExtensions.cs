using ContextR.Resolution.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContextR.Resolution;

/// <summary>
/// Extension methods for registering ContextR resolution services.
/// </summary>
public static class ContextRResolutionServiceCollectionExtensions
{
    /// <summary>
    /// Registers ContextR resolution orchestrator and default policy services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for fluent chaining.</returns>
    public static IServiceCollection AddContextRResolution(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(typeof(IContextResolutionPolicy<>), typeof(DefaultContextResolutionPolicy<>));
        services.TryAddSingleton(typeof(IContextResolutionOrchestrator<>), typeof(ContextResolutionOrchestrator<>));
        return services;
    }
}
