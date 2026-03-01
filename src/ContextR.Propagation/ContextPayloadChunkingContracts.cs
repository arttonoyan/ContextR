namespace ContextR.Propagation;

/// <summary>
/// Splits and reassembles oversized mapped payloads across multiple transport keys.
/// </summary>
/// <typeparam name="TContext">The context type being configured.</typeparam>
public interface IContextPayloadChunkingStrategy<TContext>
    where TContext : class
{
    /// <summary>
    /// Splits a payload into transport key/value pairs derived from <paramref name="key"/>.
    /// </summary>
    IEnumerable<KeyValuePair<string, string>> Chunk(string key, string payload, int maxPayloadBytes);

    /// <summary>
    /// Attempts to reassemble payload chunks for <paramref name="key"/> from the carrier.
    /// </summary>
    bool TryReassemble<TCarrier>(
        string key,
        TCarrier carrier,
        Func<TCarrier, string, string?> getter,
        out string? payload);
}
