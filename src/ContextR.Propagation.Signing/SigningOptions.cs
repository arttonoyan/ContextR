namespace ContextR.Propagation.Signing;

/// <summary>
/// Configuration options for context signing.
/// </summary>
public sealed class SigningOptions
{
    private readonly Dictionary<int, byte[]> _keys = [];

    /// <summary>
    /// Sets a single HMAC key for signing and verification.
    /// When set, no <see cref="ISigningKeyProvider"/> registration is needed.
    /// </summary>
    public byte[]? Key
    {
        get => _keys.TryGetValue(CurrentKeyVersion, out var k) ? k : null;
        set
        {
            if (value is not null)
            {
                _keys[1] = value;
                CurrentKeyVersion = 1;
            }
        }
    }

    /// <summary>
    /// The key version used for signing new signatures.
    /// During verification, the version is read from the signature header.
    /// Defaults to <c>1</c>.
    /// </summary>
    public int CurrentKeyVersion { get; set; } = 1;

    /// <summary>
    /// Logical key identifier resolved through <see cref="ISigningKeyProvider"/>.
    /// Only required when using a custom <see cref="ISigningKeyProvider"/> instead of inline keys.
    /// </summary>
    public string? KeyId { get; set; }

    /// <summary>
    /// Header name used for the HMAC signature.
    /// Defaults to <c>X-Context-Signature</c>.
    /// </summary>
    public string SignatureHeader { get; set; } = "X-Context-Signature";

    /// <summary>
    /// Adds a versioned key for key rotation scenarios.
    /// When keys are added inline, no <see cref="ISigningKeyProvider"/> registration is needed.
    /// </summary>
    /// <param name="version">Key version number.</param>
    /// <param name="key">HMAC key bytes (recommended: 32 bytes for HMAC-SHA256).</param>
    public void AddKey(int version, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version), "Key version must be >= 1.");
        _keys[version] = key;
    }

    internal bool HasInlineKeys => _keys.Count > 0;

    internal IReadOnlyDictionary<int, byte[]> InlineKeys => _keys;
}
