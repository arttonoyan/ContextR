using System.Reflection;
using System.Text.Json;

namespace ContextR.Propagation.InlineJson;

/// <summary>
/// JSON serializer strategy for non-primitive mapped properties.
/// </summary>
public sealed class InlineJsonPayloadSerializer<TContext> : IContextPayloadSerializer<TContext>
    where TContext : class
{
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Creates a serializer with optional custom JSON settings.
    /// </summary>
    public InlineJsonPayloadSerializer(JsonSerializerOptions? jsonOptions = null)
    {
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    /// <inheritdoc />
    public bool CanHandle(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return !IsSimpleTransportType(type);
    }

    /// <inheritdoc />
    public string Serialize(object value, Type propertyType)
    {
        return JsonSerializer.Serialize(value, propertyType, _jsonOptions);
    }

    /// <inheritdoc />
    public bool TryDeserialize(string payload, Type propertyType, out object? value)
    {
        try
        {
            value = JsonSerializer.Deserialize(payload, propertyType, _jsonOptions);
            return true;
        }
        catch (JsonException)
        {
            value = null;
            return false;
        }
        catch (NotSupportedException)
        {
            value = null;
            return false;
        }
    }

    private static bool IsSimpleTransportType(Type type)
    {
        if (type == typeof(string))
            return true;

        if (type.IsEnum)
            return true;

        if (type.GetMethod(
                "TryParse",
                BindingFlags.Public | BindingFlags.Static,
                [typeof(string), typeof(IFormatProvider), type.MakeByRefType()]) is not null)
            return true;

        return IsConvertiblePrimitive(type);
    }

    private static bool IsConvertiblePrimitive(Type type)
    {
        if (typeof(IConvertible).IsAssignableFrom(type))
            return true;

        return type == typeof(DateTimeOffset)
               || type == typeof(TimeSpan)
               || type == typeof(Guid);
    }
}
