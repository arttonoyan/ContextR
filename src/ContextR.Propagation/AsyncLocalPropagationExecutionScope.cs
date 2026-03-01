namespace ContextR.Propagation;

/// <summary>
/// Default propagation execution scope backed by <see cref="System.Threading.AsyncLocal{T}"/>.
/// </summary>
public sealed class AsyncLocalPropagationExecutionScope : IPropagationExecutionScope
{
    /// <inheritdoc />
    public string? CurrentDomain => PropagationExecutionContext.CurrentDomain;

    /// <inheritdoc />
    public IDisposable BeginDomainScope(string? domain) => PropagationExecutionContext.BeginDomainScope(domain);
}
