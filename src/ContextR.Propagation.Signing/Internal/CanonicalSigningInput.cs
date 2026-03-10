using System.Text;

namespace ContextR.Propagation.Signing.Internal;

internal static class CanonicalSigningInput
{
    /// <summary>
    /// Builds a deterministic signing input from key/value pairs.
    /// Keys are sorted using ordinal (byte-order) comparison.
    /// Format: "key1=value1\nkey2=value2\n" (trailing newline).
    /// </summary>
    internal static byte[] Build(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var sorted = pairs
            .OrderBy(static kv => kv.Key, StringComparer.Ordinal)
            .ToArray();

        var sb = new StringBuilder();
        foreach (var kv in sorted)
        {
            sb.Append(kv.Key);
            sb.Append('=');
            sb.Append(kv.Value);
            sb.Append('\n');
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
