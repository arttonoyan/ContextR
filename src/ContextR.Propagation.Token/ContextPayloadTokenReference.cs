namespace ContextR.Propagation.Token;

/// <summary>
/// Token reference envelope for large payload transport.
/// </summary>
/// <param name="Token">Store token identifier.</param>
/// <param name="Version">Optional token envelope version.</param>
public sealed record ContextPayloadTokenReference(string Token, string? Version = null);
