namespace ContextR.Resolution;

/// <summary>
/// Describes the trust boundary of the current ingress operation.
/// </summary>
public enum ContextIngressBoundary
{
    /// <summary>
    /// Request entered from an external/untrusted boundary.
    /// </summary>
    External = 0,

    /// <summary>
    /// Request entered from an internal/trusted service boundary.
    /// </summary>
    Internal = 1
}
