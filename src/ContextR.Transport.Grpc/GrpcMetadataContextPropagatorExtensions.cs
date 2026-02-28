using Grpc.Core;
using ContextR.Propagation;

namespace ContextR.Transport.Grpc;

/// <summary>
/// Helper methods for using <see cref="IContextPropagator{TContext}"/> with gRPC <see cref="Metadata"/>.
/// </summary>
public static class GrpcMetadataContextPropagatorExtensions
{
    /// <summary>
    /// Creates gRPC metadata for the provided context.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="propagator">The context propagator.</param>
    /// <param name="context">The context value to inject.</param>
    /// <returns>A <see cref="Metadata"/> containing propagated key-value pairs.</returns>
    public static Metadata CreateMetadata<TContext>(
        this IContextPropagator<TContext> propagator,
        TContext context)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(propagator);
        ArgumentNullException.ThrowIfNull(context);

        var headers = new Metadata();
        propagator.Inject(
            context,
            headers,
            static (metadata, key, value) => metadata.Add(key.ToLowerInvariant(), value));
        return headers;
    }

    /// <summary>
    /// Extracts context from gRPC metadata.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="propagator">The context propagator.</param>
    /// <param name="headers">The source metadata.</param>
    /// <returns>The extracted context, or <see langword="null"/> when required values are missing.</returns>
    public static TContext? ExtractContext<TContext>(
        this IContextPropagator<TContext> propagator,
        Metadata headers)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(propagator);
        ArgumentNullException.ThrowIfNull(headers);

        return propagator.Extract(
            headers,
            static (metadata, key) => metadata.GetValue(key.ToLowerInvariant()));
    }
}
