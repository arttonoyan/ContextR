namespace ContextR;

/// <summary>
/// Defines how a context type is serialized to and deserialized from a transport carrier
/// (e.g., HTTP headers, gRPC metadata, Kafka headers).
/// <para>
/// Implement once per context type. The same propagator works across all transports
/// because it operates on a generic carrier via getter/setter delegates.
/// </para>
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
public interface IContextPropagator<TContext> where TContext : class
{
    /// <summary>
    /// Writes context values into a carrier using the provided setter.
    /// </summary>
    /// <typeparam name="TCarrier">The carrier type (e.g., <c>HttpRequestHeaders</c>, <c>Metadata</c>).</typeparam>
    /// <param name="context">The context to serialize.</param>
    /// <param name="carrier">The target carrier to write key-value pairs into.</param>
    /// <param name="setter">A delegate that writes a key-value pair to the carrier.</param>
    void Inject<TCarrier>(TContext context, TCarrier carrier, Action<TCarrier, string, string> setter);

    /// <summary>
    /// Reads context values from a carrier using the provided getter.
    /// </summary>
    /// <typeparam name="TCarrier">The carrier type (e.g., <c>IHeaderDictionary</c>, <c>Metadata</c>).</typeparam>
    /// <param name="carrier">The source carrier to read key-value pairs from.</param>
    /// <param name="getter">A delegate that reads a value by key from the carrier, returning <see langword="null"/> when not found.</param>
    /// <returns>The deserialized context, or <see langword="null"/> when required values are missing.</returns>
    TContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, string?> getter);
}
