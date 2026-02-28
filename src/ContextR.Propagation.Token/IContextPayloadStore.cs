namespace ContextR.Propagation.Token;

/// <summary>
/// Store contract for token-based payload transport.
/// </summary>
public interface IContextPayloadStore
{
    /// <summary>
    /// Stores payload and returns token reference identifier.
    /// </summary>
    Task<string> PutAsync(string payload, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads payload by token reference.
    /// </summary>
    Task<string?> GetAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes payload by token reference.
    /// </summary>
    Task DeleteAsync(string token, CancellationToken cancellationToken = default);
}
