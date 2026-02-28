namespace ContextR.Propagation.Token;

/// <summary>
/// Encodes/decodes token references into transport-safe values.
/// </summary>
public interface IContextPayloadTokenCodec
{
    /// <summary>
    /// Encodes a token reference to a transport value.
    /// </summary>
    string Encode(ContextPayloadTokenReference reference);

    /// <summary>
    /// Attempts to decode a transport value into token reference.
    /// </summary>
    bool TryDecode(string value, out ContextPayloadTokenReference? reference);
}
