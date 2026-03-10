using System.Diagnostics.CodeAnalysis;

namespace ContextR.Propagation.Signing.Internal;

internal static class SignatureCodec
{
    private const char Separator = '.';

    /// <summary>
    /// Encodes an HMAC signature and key version into a header-safe string.
    /// Format: "base64url-hmac.keyVersion"
    /// </summary>
    internal static string Encode(byte[] hmac, int keyVersion)
    {
        var base64 = Convert.ToBase64String(hmac);
        var base64Url = base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return $"{base64Url}{Separator}{keyVersion}";
    }

    /// <summary>
    /// Attempts to decode a signature header value into HMAC bytes and key version.
    /// </summary>
    internal static bool TryDecode(
        string headerValue,
        [NotNullWhen(true)] out byte[]? hmac,
        out int keyVersion)
    {
        hmac = null;
        keyVersion = 0;

        var separatorIndex = headerValue.LastIndexOf(Separator);
        if (separatorIndex < 1 || separatorIndex >= headerValue.Length - 1)
            return false;

        var base64UrlPart = headerValue.AsSpan(0, separatorIndex);
        var versionPart = headerValue.AsSpan(separatorIndex + 1);

        if (!int.TryParse(versionPart, out keyVersion))
            return false;

        try
        {
            var padded = base64UrlPart.ToString()
                .Replace('-', '+')
                .Replace('_', '/');

            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }

            hmac = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
