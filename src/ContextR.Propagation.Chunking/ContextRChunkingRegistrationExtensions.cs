using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContextR.Propagation.Chunking;

/// <summary>
/// Extension methods for registering payload chunking strategy.
/// </summary>
public static class ContextRChunkingRegistrationExtensions
{
    /// <summary>
    /// Registers default UTF-8-safe chunking strategy for mapped payloads.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UseChunkingPayloads<TContext>(
        this IContextRegistrationBuilder<TContext> builder)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddSingleton<IContextPayloadChunkingStrategy<TContext>, DefaultPayloadChunkingStrategy<TContext>>();
        return builder;
    }
}
