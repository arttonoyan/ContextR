using System.Threading;

namespace ContextR.Propagation;

/// <summary>
/// Carries the current propagation domain during inject/extract execution.
/// </summary>
public static class PropagationExecutionContext
{
    private const string DefaultDomainKeyValue = "__default__";
    private static readonly AsyncLocal<string?> CurrentDomainSlot = new();

    internal static string DefaultDomainKey => DefaultDomainKeyValue;

    /// <summary>
    /// Gets the current execution domain for propagation.
    /// </summary>
    public static string? CurrentDomain => CurrentDomainSlot.Value;

    /// <summary>
    /// Begins a scope that sets current propagation domain.
    /// </summary>
    public static IDisposable BeginDomainScope(string? domain)
    {
        var previous = CurrentDomainSlot.Value;
        CurrentDomainSlot.Value = domain;
        return new DomainScope(previous);
    }

    private sealed class DomainScope(string? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            CurrentDomainSlot.Value = previous;
            _disposed = true;
        }
    }
}
