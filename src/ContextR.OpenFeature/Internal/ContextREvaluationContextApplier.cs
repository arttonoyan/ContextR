using Microsoft.Extensions.DependencyInjection;
using OpenFeature.Model;

namespace ContextR.OpenFeature.Internal;

internal static class ContextREvaluationContextApplier
{
    public static void Apply(
        EvaluationContextBuilder contextBuilder,
        IServiceProvider serviceProvider,
        ContextROpenFeatureOptions options)
    {
        var attributes = new Dictionary<string, Value>(StringComparer.Ordinal);
        var accessor = serviceProvider.GetRequiredService<IContextAccessor>();

        if (options.TargetingKeyFactory is not null)
        {
            var targetingKey = options.TargetingKeyFactory(serviceProvider);
            if (!string.IsNullOrWhiteSpace(targetingKey))
            {
                contextBuilder.SetTargetingKey(targetingKey);
            }
        }

        if (options.ContextKindFactory is not null)
        {
            var kind = options.ContextKindFactory(serviceProvider);
            if (!string.IsNullOrWhiteSpace(kind))
            {
                WriteAttribute(options, attributes, "kind", kind);
            }
        }

        foreach (var registration in options.Registrations)
        {
            var context = GetContext(accessor, registration.ContextType, registration.Domain);
            if (context is null)
                continue;

            foreach (var explicitMap in registration.ExplicitMappings)
            {
                var rawValue = explicitMap.Value(context);
                WriteAttribute(options, attributes, explicitMap.Key, rawValue);
            }

            if (!registration.IsConventionEnabled)
                continue;

            foreach (var property in registration.ConventionProperties)
            {
                if (registration.IgnoredMembers.Contains(property.MemberName))
                    continue;

                var rawValue = property.Getter(context);
                WriteAttribute(options, attributes, property.AttributeKey, rawValue);
            }
        }

        foreach (var attribute in attributes)
        {
            contextBuilder.Set(attribute.Key, attribute.Value);
        }
    }

    private static object? GetContext(IContextAccessor accessor, Type contextType, string? domain)
    {
        var method = domain is null
            ? typeof(IContextAccessor).GetMethod(nameof(IContextAccessor.GetContext), Type.EmptyTypes)
            : typeof(IContextAccessor).GetMethod(nameof(IContextAccessor.GetContext), [typeof(string)]);

        if (method is null)
            return null;

        var generic = method.MakeGenericMethod(contextType);
        return domain is null
            ? generic.Invoke(accessor, null)
            : generic.Invoke(accessor, [domain]);
    }

    private static void WriteAttribute(
        ContextROpenFeatureOptions options,
        IDictionary<string, Value> attributes,
        string key,
        object? rawValue)
    {
        if (!IsAllowed(options, key))
            return;

        if (rawValue is null && !options.IncludeNullValues)
            return;

        if (!TryConvert(rawValue, out var value))
        {
            if (options.UnsupportedValueBehavior == ContextROpenFeatureUnsupportedValueBehavior.Throw)
            {
                throw new InvalidOperationException($"Mapped key '{key}' cannot be converted to OpenFeature Value.");
            }

            return;
        }

        if (attributes.ContainsKey(key) && options.CollisionBehavior == ContextROpenFeatureCollisionBehavior.Throw)
        {
            throw new InvalidOperationException($"Mapped key '{key}' is defined more than once.");
        }

        attributes[key] = value;
    }

    private static bool IsAllowed(ContextROpenFeatureOptions options, string key)
    {
        if (options.BlockedKeys.Contains(key))
            return false;

        if (options.AllowedKeys.Count == 0)
            return true;

        return options.AllowedKeys.Contains(key);
    }

    private static bool TryConvert(object? value, out Value converted)
    {
        if (value is null)
        {
            converted = new Value();
            return true;
        }

        switch (value)
        {
            case Value val:
                converted = val;
                return true;
            case string str:
                converted = new Value(str);
                return true;
            case bool b:
                converted = new Value(b);
                return true;
            case int i:
                converted = new Value(i);
                return true;
            case short s:
                converted = new Value((int)s);
                return true;
            case byte bt:
                converted = new Value((int)bt);
                return true;
            case long l:
                converted = new Value(Convert.ToDouble(l));
                return true;
            case float f:
                converted = new Value(Convert.ToDouble(f));
                return true;
            case double d:
                converted = new Value(d);
                return true;
            case decimal m:
                converted = new Value(Convert.ToDouble(m));
                return true;
            case DateTime dt:
                converted = new Value(dt);
                return true;
            case Guid guid:
                converted = new Value(guid.ToString("D"));
                return true;
            case Enum enumValue:
                converted = new Value(enumValue.ToString());
                return true;
            case global::OpenFeature.Model.Structure structure:
                converted = new Value(structure);
                return true;
            case IDictionary<string, object?> dict:
                converted = new Value(ToStructure(dict));
                return true;
            case IEnumerable<object?> sequence:
                converted = new Value(sequence.Select(static item => ToValue(item)).ToList());
                return true;
            default:
                converted = default!;
                return false;
        }
    }

    private static global::OpenFeature.Model.Structure ToStructure(IDictionary<string, object?> dictionary)
    {
        var builder = global::OpenFeature.Model.Structure.Builder();
        foreach (var kvp in dictionary)
        {
            builder.Set(kvp.Key, ToValue(kvp.Value));
        }

        return builder.Build();
    }

    private static Value ToValue(object? value)
    {
        if (!TryConvert(value, out var converted))
        {
            return new Value(value?.ToString() ?? string.Empty);
        }

        return converted;
    }
}
