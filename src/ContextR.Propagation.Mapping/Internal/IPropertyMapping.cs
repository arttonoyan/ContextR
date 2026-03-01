namespace ContextR.Propagation.Internal;

internal interface IPropertyMapping<TContext> where TContext : class
{
    string Key { get; }
    bool IsRequired { get; }
    IEnumerable<KeyValuePair<string, string>> GetValues(TContext context);
    string? GetRawValue<TCarrier>(TCarrier carrier, Func<TCarrier, string, string?> getter);
    bool TrySetValue(TContext context, string value);
}
