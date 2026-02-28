namespace ContextR.Propagation.Internal;

internal interface IPropertyMapping<TContext> where TContext : class
{
    string Key { get; }
    bool IsRequired { get; }
    string? GetValue(TContext context);
    bool TrySetValue(TContext context, string value);
}
