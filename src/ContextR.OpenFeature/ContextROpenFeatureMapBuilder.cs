using System.Linq.Expressions;
using System.Reflection;

namespace ContextR.OpenFeature;

/// <summary>
/// Fluent map builder that projects a ContextR context type into OpenFeature attributes.
/// </summary>
/// <typeparam name="TContext">ContextR context type.</typeparam>
public sealed class ContextROpenFeatureMapBuilder<TContext>
    where TContext : class
{
    private readonly Dictionary<string, Func<TContext, object?>> _explicitMappings = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ignoredMembers = new(StringComparer.Ordinal);

    internal bool IsConventionEnabled { get; private set; }

    internal string? ConventionPrefix { get; private set; }

    internal IReadOnlyDictionary<string, Func<TContext, object?>> ExplicitMappings => _explicitMappings;

    internal IReadOnlySet<string> IgnoredMembers => _ignoredMembers;

    /// <summary>
    /// Maps a selected property to an OpenFeature EvaluationContext attribute key.
    /// </summary>
    public ContextROpenFeatureMapBuilder<TContext> MapProperty<TProperty>(
        Expression<Func<TContext, TProperty>> property,
        string key)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var compiled = property.Compile();
        _explicitMappings[key] = context => compiled(context);
        return this;
    }

    /// <summary>
    /// Ignores the selected property when convention mapping is enabled.
    /// </summary>
    public ContextROpenFeatureMapBuilder<TContext> Ignore<TProperty>(
        Expression<Func<TContext, TProperty>> property)
    {
        ArgumentNullException.ThrowIfNull(property);

        var memberName = ResolveMemberName(property);
        _ignoredMembers.Add(memberName);
        return this;
    }

    /// <summary>
    /// Enables convention mapping for all readable public properties.
    /// </summary>
    /// <param name="prefix">Optional attribute key prefix.</param>
    public ContextROpenFeatureMapBuilder<TContext> ByConvention(string? prefix = null)
    {
        IsConventionEnabled = true;
        ConventionPrefix = prefix;
        return this;
    }

    internal static IReadOnlyList<PropertyInfo> GetConventionProperties()
    {
        return typeof(TContext)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();
    }

    private static string ResolveMemberName<TProperty>(Expression<Func<TContext, TProperty>> expression)
    {
        if (expression.Body is MemberExpression member && member.Member is PropertyInfo)
            return member.Member.Name;

        if (expression.Body is UnaryExpression unary &&
            unary.Operand is MemberExpression unaryMember &&
            unaryMember.Member is PropertyInfo)
        {
            return unaryMember.Member.Name;
        }

        throw new ArgumentException("The expression must select a property.", nameof(expression));
    }
}
