namespace ContextR.OpenFeature.Internal;

internal sealed class ContextMappingRegistration
{
    public required Type ContextType { get; init; }

    public required string? Domain { get; init; }

    public required IReadOnlyDictionary<string, Func<object, object?>> ExplicitMappings { get; init; }

    public required IReadOnlySet<string> IgnoredMembers { get; init; }

    public required bool IsConventionEnabled { get; init; }

    public required string? ConventionPrefix { get; init; }

    public required IReadOnlyList<ContextPropertyAccessor> ConventionProperties { get; init; }

    public static ContextMappingRegistration Create<TContext>(
        string? domain,
        ContextROpenFeatureMapBuilder<TContext> builder)
        where TContext : class
    {
        var explicitMappings = builder.ExplicitMappings.ToDictionary(
            static kvp => kvp.Key,
            static kvp => new Func<object, object?>(instance => kvp.Value((TContext)instance)),
            StringComparer.Ordinal);

        var conventionProperties = ContextROpenFeatureMapBuilder<TContext>
            .GetConventionProperties()
            .Select(p => new ContextPropertyAccessor(
                p.Name,
                ComposeAttributeKey(builder.ConventionPrefix, p.Name),
                p.GetValue))
            .ToArray();

        return new ContextMappingRegistration
        {
            ContextType = typeof(TContext),
            Domain = domain,
            ExplicitMappings = explicitMappings,
            IgnoredMembers = new HashSet<string>(builder.IgnoredMembers, StringComparer.Ordinal),
            IsConventionEnabled = builder.IsConventionEnabled,
            ConventionPrefix = builder.ConventionPrefix,
            ConventionProperties = conventionProperties
        };
    }

    private static string ComposeAttributeKey(string? prefix, string memberName)
    {
        return string.IsNullOrWhiteSpace(prefix) ? memberName : $"{prefix}{memberName}";
    }
}

internal sealed record ContextPropertyAccessor(
    string MemberName,
    string AttributeKey,
    Func<object, object?> Getter);
