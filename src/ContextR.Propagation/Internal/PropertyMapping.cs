using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace ContextR.Propagation.Internal;

internal static class PropertyMapping
{
    public static IPropertyMapping<TContext> Create<TContext, TProperty>(
        Expression<Func<TContext, TProperty>> propertyExpression,
        string key)
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

        if (!propertyInfo.CanWrite)
            throw new ArgumentException($"Property '{propertyInfo.Name}' must be writable.", nameof(propertyExpression));

        var getter = propertyExpression.Compile();
        var setterParam = Expression.Parameter(typeof(TContext));
        var valueParam = Expression.Parameter(typeof(TProperty));
        var setter = Expression.Lambda<Action<TContext, TProperty>>(
            Expression.Assign(Expression.Property(setterParam, propertyInfo), valueParam),
            setterParam, valueParam).Compile();

        return new PropertyMapping<TContext, TProperty>(key, getter, setter);
    }
}

internal sealed class PropertyMapping<TContext, TProperty> : IPropertyMapping<TContext>
    where TContext : class
{
    private readonly Func<TContext, TProperty> _getter;
    private readonly Action<TContext, TProperty> _setter;

    public PropertyMapping(string key, Func<TContext, TProperty> getter, Action<TContext, TProperty> setter)
    {
        Key = key;
        _getter = getter;
        _setter = setter;
    }

    public string Key { get; }

    public string? GetValue(TContext context)
    {
        var value = _getter(context);
        return value?.ToString();
    }

    public bool TrySetValue(TContext context, string value)
    {
        try
        {
            if (!TryParse(value, out var parsed))
                return false;

            _setter(context, parsed);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParse(string value, [NotNullWhen(true)] out TProperty? result)
    {
        var type = Nullable.GetUnderlyingType(typeof(TProperty)) ?? typeof(TProperty);

        if (type == typeof(string))
        {
            result = (TProperty)(object)value;
            return true;
        }

        var tryParseMethod = type.GetMethod(
            "TryParse",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(string), typeof(IFormatProvider), type.MakeByRefType()]);

        if (tryParseMethod is not null)
        {
            var args = new object?[] { value, null, null };
            var success = (bool)tryParseMethod.Invoke(null, args)!;
            if (success)
            {
                result = (TProperty)args[2]!;
                return true;
            }

            result = default;
            return false;
        }

        result = (TProperty)Convert.ChangeType(value, type);
        return true;
    }
}
