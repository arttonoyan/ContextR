namespace ContextR;

/// <summary>
/// Configures how the default (parameterless) context operations resolve to a specific domain.
/// </summary>
public sealed class ContextDomainPolicy
{
    /// <summary>
    /// A factory that selects the default domain at runtime.
    /// When set, parameterless <c>GetContext</c> and <c>SetContext</c> calls
    /// delegate to the domain returned by this selector.
    /// When <see langword="null"/>, parameterless calls use the domainless storage slot.
    /// </summary>
    public Func<IServiceProvider, string?>? DefaultDomainSelector { get; set; }
}
