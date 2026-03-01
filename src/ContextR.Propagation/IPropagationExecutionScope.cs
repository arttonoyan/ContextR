namespace ContextR.Propagation;

/// <summary>
/// Provides ambient execution scope information for propagation operations.
/// </summary>
public interface IPropagationExecutionScope
{
    /// <summary>
    /// Gets the current propagation domain for the async flow.
    /// </summary>
    string? CurrentDomain { get; }

    /// <summary>
    /// Begins a domain scope for the current async flow.
    /// </summary>
    /// <param name="domain">The domain name or <see langword="null"/> for default scope.</param>
    /// <returns>A disposable that restores the previous domain when disposed.</returns>
    IDisposable BeginDomainScope(string? domain);
}
