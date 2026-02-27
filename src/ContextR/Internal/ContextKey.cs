namespace ContextR.Internal;

internal readonly record struct ContextKey(string? Domain, Type ContextType);
