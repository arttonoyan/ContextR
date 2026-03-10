using System.Security.Cryptography;

namespace ContextR.Propagation.Signing.UnitTests.Infrastructure;

public sealed class StaticSigningKeyProvider : ISigningKeyProvider
{
    private readonly Dictionary<(string KeyId, int Version), byte[]> _keys = new();
    private readonly Dictionary<string, int> _currentVersions = new();

    public StaticSigningKeyProvider(string keyId, int version, byte[]? key = null)
    {
        key ??= RandomNumberGenerator.GetBytes(32);
        _keys[(keyId, version)] = key;
        _currentVersions[keyId] = version;
    }

    public void AddKey(string keyId, int version, byte[] key, bool makeCurrent = false)
    {
        _keys[(keyId, version)] = key;
        if (makeCurrent)
            _currentVersions[keyId] = version;
    }

    public byte[] GetKey(string keyId, int version)
    {
        if (!_keys.TryGetValue((keyId, version), out var key))
            throw new KeyNotFoundException($"Key '{keyId}' version {version} not found.");
        return key;
    }

    public int GetCurrentVersion(string keyId)
    {
        if (!_currentVersions.TryGetValue(keyId, out var version))
            throw new KeyNotFoundException($"Key '{keyId}' not found.");
        return version;
    }
}
