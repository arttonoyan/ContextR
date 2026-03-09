namespace ContextR.Internal;

internal readonly record struct ContextKey
{
    public string? Domain { get; }
    public Type ContextType { get; }

    public ContextKey(string? domain, Type contextType)
    {
        ArgumentNullException.ThrowIfNull(contextType);
        Domain = domain;
        ContextType = contextType;
    }
}
