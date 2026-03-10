namespace ContextR.Propagation.Signing.Internal;

internal sealed class InMemorySigningKeyProvider : ISigningKeyProvider
{
    private readonly IReadOnlyDictionary<int, byte[]> _keys;
    private readonly int _currentVersion;

    internal InMemorySigningKeyProvider(IReadOnlyDictionary<int, byte[]> keys, int currentVersion)
    {
        _keys = keys;
        _currentVersion = currentVersion;
    }

    public byte[] GetKey(string keyId, int version)
    {
        if (!_keys.TryGetValue(version, out var key))
            throw new KeyNotFoundException($"Signing key version {version} not found.");
        return key;
    }

    public int GetCurrentVersion(string keyId) => _currentVersion;
}
