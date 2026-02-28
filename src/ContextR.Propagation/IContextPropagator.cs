namespace ContextR.Propagation;

/// <summary>
/// Defines how a context type is serialized to and deserialized from a transport carrier
/// (e.g., HTTP headers, gRPC metadata).
/// </summary>
/// <typeparam name="TContext">The context type being propagated.</typeparam>
public interface IContextPropagator<TContext>
    where TContext : class
{
    /// <summary>
    /// Injects context values into the given carrier using the supplied setter callback.
    /// </summary>
    /// <typeparam name="TCarrier">The carrier type (e.g., <c>HttpRequestHeaders</c>, <c>Metadata</c>).</typeparam>
    /// <param name="context">The context to serialize.</param>
    /// <param name="carrier">The target carrier instance.</param>
    /// <param name="setter">A callback that writes key/value pairs to the carrier.</param>
    void Inject<TCarrier>(TContext context, TCarrier carrier, Action<TCarrier, string, string> setter);

    /// <summary>
    /// Extracts a context instance from the given carrier using the supplied getter callback.
    /// </summary>
    /// <typeparam name="TCarrier">The carrier type (e.g., <c>IHeaderDictionary</c>, <c>Metadata</c>).</typeparam>
    /// <param name="carrier">The source carrier instance.</param>
    /// <param name="getter">A callback that reads values by key from the carrier.</param>
    /// <returns>The deserialized context, or <see langword="null"/> when required values are missing.</returns>
    TContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, string?> getter);
}
