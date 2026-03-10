namespace ContextR.Propagation.Signing;

/// <summary>
/// Provides HMAC key material for context signing and verification.
/// </summary>
public interface ISigningKeyProvider
{
    /// <summary>
    /// Returns the HMAC key bytes for the given key identifier and version.
    /// </summary>
    /// <param name="keyId">Logical key identifier.</param>
    /// <param name="version">Key version. During verification the version comes from the signature header.</param>
    byte[] GetKey(string keyId, int version);

    /// <summary>
    /// Returns the current (latest) key version for the given key identifier.
    /// Used when signing; during verification the version is read from the signature header.
    /// </summary>
    int GetCurrentVersion(string keyId);
}
