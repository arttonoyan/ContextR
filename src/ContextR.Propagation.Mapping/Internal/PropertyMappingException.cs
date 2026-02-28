using ContextR.Propagation;

namespace ContextR.Propagation.Internal;

internal sealed class PropertyMappingException(
    PropagationFailureReason reason,
    string message,
    Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public PropagationFailureReason Reason { get; } = reason;
}
