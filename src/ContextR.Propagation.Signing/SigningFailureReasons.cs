namespace ContextR.Propagation.Signing;

/// <summary>
/// Well-known failure reason constants for context signing operations.
/// </summary>
public static class SigningFailureReasons
{
    /// <summary>
    /// The HMAC signature did not match the expected value.
    /// </summary>
    public const string SignatureInvalid = "SignatureInvalid";

    /// <summary>
    /// The signature header was absent from the carrier.
    /// </summary>
    public const string SignatureMissing = "SignatureMissing";

    /// <summary>
    /// The signature header value could not be parsed.
    /// </summary>
    public const string SignatureMalformed = "SignatureMalformed";

    /// <summary>
    /// The key provider could not resolve the requested key identifier or version.
    /// </summary>
    public const string KeyNotFound = "KeyNotFound";
}
