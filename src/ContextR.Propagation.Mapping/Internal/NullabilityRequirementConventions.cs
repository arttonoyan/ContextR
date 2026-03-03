using System.Linq.Expressions;
using System.Reflection;
using ContextR.Propagation.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Propagation.Internal;

internal sealed class NullabilityConventionOptions<TContext>
    where TContext : class
{
    public bool Enabled { get; set; } = true;
}

internal static class NullabilityRequirementConventions
{
    private static readonly NullabilityInfoContext NullabilityContext = new();
    private static readonly object NullabilityContextLock = new();

    internal static NullabilityConventionOptions<TContext> GetOrAddOptions<TContext>(IServiceCollection services)
        where TContext : class
    {
        var existing = services
            .FirstOrDefault(d => d.ServiceType == typeof(NullabilityConventionOptions<TContext>))
            ?.ImplementationInstance as NullabilityConventionOptions<TContext>;

        if (existing is not null)
            return existing;

        var created = new NullabilityConventionOptions<TContext>();
        services.AddSingleton(created);
        return created;
    }

    internal static bool IsEnabled<TContext>(IServiceCollection services)
        where TContext : class
    {
        var options = services
            .FirstOrDefault(d => d.ServiceType == typeof(NullabilityConventionOptions<TContext>))
            ?.ImplementationInstance as NullabilityConventionOptions<TContext>;

        return options?.Enabled ?? true;
    }

    internal static PropertyRequirement ResolveRequirement<TContext, TProperty>(
        Expression<Func<TContext, TProperty>> propertyExpression)
        where TContext : class
    {
        var member = propertyExpression.Body as MemberExpression
            ?? throw new ArgumentException(
                $"Expression must be a property access (e.g., c => c.PropertyName), got {propertyExpression.Body.NodeType}.",
                nameof(propertyExpression));

        var propertyInfo = member.Member as PropertyInfo
            ?? throw new ArgumentException(
                $"Expression must reference a property, got {member.Member.MemberType}.",
                nameof(propertyExpression));

        return ResolveRequirement(propertyInfo);
    }

    private static PropertyRequirement ResolveRequirement(PropertyInfo propertyInfo)
    {
        var propertyType = propertyInfo.PropertyType;
        if (propertyType.IsValueType)
            return Nullable.GetUnderlyingType(propertyType) is null
                ? PropertyRequirement.Required
                : PropertyRequirement.Optional;

        NullabilityState nullability;
        lock (NullabilityContextLock)
        {
            nullability = NullabilityContext.Create(propertyInfo).ReadState;
        }

        return nullability == NullabilityState.NotNull
            ? PropertyRequirement.Required
            : PropertyRequirement.Optional;
    }
}
