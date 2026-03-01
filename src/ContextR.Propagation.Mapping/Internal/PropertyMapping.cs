using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using ContextR.Propagation.Mapping;

namespace ContextR.Propagation.Internal;

internal static class PropertyMapping
{
    public static IPropertyMapping<TContext> Create<TContext, TProperty>(
        Expression<Func<TContext, TProperty>> propertyExpression,
        string key,
        IContextPayloadSerializer<TContext>? payloadSerializer = null,
        IContextTransportPolicy<TContext>? transportPolicy = null,
        IContextPayloadChunkingStrategy<TContext>? chunkingStrategy = null,
        Func<ContextPropagationStrategyPolicyContext, ContextOversizeBehavior?>? strategyPolicyEvaluator = null,
        PropertyRequirement requirement = PropertyRequirement.Optional,
        ContextOversizeBehavior? oversizeBehaviorOverride = null)
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

        return new PropertyMapping<TContext, TProperty>(
            key,
            getter,
            setter,
            payloadSerializer,
            transportPolicy,
            chunkingStrategy,
            strategyPolicyEvaluator,
            requirement,
            oversizeBehaviorOverride);
    }
}

internal sealed class PropertyMapping<TContext, TProperty> : IPropertyMapping<TContext>
    where TContext : class
{
    private readonly Func<TContext, TProperty> _getter;
    private readonly Action<TContext, TProperty> _setter;
    private readonly IContextPayloadSerializer<TContext>? _payloadSerializer;
    private readonly IContextTransportPolicy<TContext>? _transportPolicy;
    private readonly IContextPayloadChunkingStrategy<TContext>? _chunkingStrategy;
    private readonly Func<ContextPropagationStrategyPolicyContext, ContextOversizeBehavior?>? _strategyPolicyEvaluator;
    private readonly ContextOversizeBehavior? _oversizeBehaviorOverride;

    public PropertyMapping(
        string key,
        Func<TContext, TProperty> getter,
        Action<TContext, TProperty> setter,
        IContextPayloadSerializer<TContext>? payloadSerializer,
        IContextTransportPolicy<TContext>? transportPolicy,
        IContextPayloadChunkingStrategy<TContext>? chunkingStrategy,
        Func<ContextPropagationStrategyPolicyContext, ContextOversizeBehavior?>? strategyPolicyEvaluator,
        PropertyRequirement requirement,
        ContextOversizeBehavior? oversizeBehaviorOverride)
    {
        Key = key;
        _getter = getter;
        _setter = setter;
        _payloadSerializer = payloadSerializer;
        _transportPolicy = transportPolicy;
        _chunkingStrategy = chunkingStrategy;
        _strategyPolicyEvaluator = strategyPolicyEvaluator;
        _oversizeBehaviorOverride = oversizeBehaviorOverride;
        IsRequired = requirement == PropertyRequirement.Required;
    }

    public string Key { get; }
    public bool IsRequired { get; }

    public IEnumerable<KeyValuePair<string, string>> GetValues(TContext context)
    {
        var value = _getter(context);
        if (value is null)
            return [];

        if (CanUseCustomSerializer())
        {
            var serialized = _payloadSerializer!.Serialize(value, typeof(TProperty));
            if (!IsWithinLimit(serialized, out var payloadSize))
            {
                return HandleOversizeOnInject(serialized, payloadSize);
            }

            return [new KeyValuePair<string, string>(Key, serialized)];
        }

        var plain = value.ToString();
        return plain is null
            ? []
            : [new KeyValuePair<string, string>(Key, plain)];
    }

    public string? GetRawValue<TCarrier>(TCarrier carrier, Func<TCarrier, string, string?> getter)
    {
        var direct = getter(carrier, Key);
        if (direct is not null)
            return direct;

        if (ResolveOversizeBehavior(PropagationDirection.Extract) != ContextOversizeBehavior.ChunkProperty ||
            _chunkingStrategy is null)
            return null;

        return _chunkingStrategy.TryReassemble(Key, carrier, getter, out var payload)
            ? payload
            : null;
    }

    public bool TrySetValue(TContext context, string value)
    {
        try
        {
            if (CanUseCustomSerializer())
            {
                if (ResolveOversizeBehavior(PropagationDirection.Extract) != ContextOversizeBehavior.ChunkProperty &&
                    !IsWithinLimit(value, out var payloadSize))
                    return HandleOversizeOnExtract(payloadSize);

                if (!_payloadSerializer!.TryDeserialize(value, typeof(TProperty), out var parsedValue))
                    return false;

                if (parsedValue is null)
                {
                    if (default(TProperty) is null)
                    {
                        _setter(context, default!);
                        return true;
                    }

                    return false;
                }

                if (parsedValue is TProperty typed)
                {
                    _setter(context, typed);
                    return true;
                }

                return false;
            }

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

    private bool CanUseCustomSerializer()
    {
        return _payloadSerializer is not null && _payloadSerializer.CanHandle(typeof(TProperty));
    }

    private bool IsWithinLimit(string payload, out int payloadSize)
    {
        payloadSize = Encoding.UTF8.GetByteCount(payload);
        var maxPayloadBytes = _transportPolicy?.MaxPayloadBytes ?? 0;
        return maxPayloadBytes <= 0 || payloadSize <= maxPayloadBytes;
    }

    private IEnumerable<KeyValuePair<string, string>> HandleOversizeOnInject(string serializedPayload, int payloadSize)
    {
        var strategy = ResolveOversizeBehavior(PropagationDirection.Inject, payloadSize);
        var maxPayloadBytes = _transportPolicy?.MaxPayloadBytes ?? 0;

        return strategy switch
        {
            ContextOversizeBehavior.SkipProperty => [],
            ContextOversizeBehavior.FailFast => throw new PropertyMappingException(
                PropagationFailureReason.Oversize,
                $"Mapped payload for key '{Key}' exceeded limit ({payloadSize} bytes > {maxPayloadBytes} bytes)."),
            ContextOversizeBehavior.FallbackToToken => throw new PropertyMappingException(
                PropagationFailureReason.TokenFallbackUnavailable,
                $"Mapped payload for key '{Key}' exceeded limit ({payloadSize} bytes > {maxPayloadBytes} bytes) and requested token fallback, but no token strategy is configured."),
            ContextOversizeBehavior.ChunkProperty when _chunkingStrategy is not null => _chunkingStrategy.Chunk(Key, serializedPayload, maxPayloadBytes),
            ContextOversizeBehavior.ChunkProperty => throw new PropertyMappingException(
                PropagationFailureReason.Oversize,
                $"Mapped payload for key '{Key}' requested chunking, but no chunking strategy is configured."),
            _ => throw new InvalidOperationException("Unsupported oversize behavior.")
        };
    }

    private bool HandleOversizeOnExtract(int payloadSize)
    {
        var strategy = ResolveOversizeBehavior(PropagationDirection.Extract, payloadSize);
        var maxPayloadBytes = _transportPolicy?.MaxPayloadBytes ?? 0;

        return strategy switch
        {
            ContextOversizeBehavior.SkipProperty => false,
            ContextOversizeBehavior.FailFast => throw new PropertyMappingException(
                PropagationFailureReason.Oversize,
                $"Mapped payload for key '{Key}' exceeded limit ({payloadSize} bytes > {maxPayloadBytes} bytes)."),
            ContextOversizeBehavior.FallbackToToken => throw new PropertyMappingException(
                PropagationFailureReason.TokenFallbackUnavailable,
                $"Mapped payload for key '{Key}' exceeded limit ({payloadSize} bytes > {maxPayloadBytes} bytes) and requested token fallback, but no token strategy is configured."),
            ContextOversizeBehavior.ChunkProperty => false,
            _ => throw new InvalidOperationException("Unsupported oversize behavior.")
        };
    }

    private ContextOversizeBehavior ResolveOversizeBehavior(PropagationDirection direction, int? payloadSizeBytes = null)
    {
        return _oversizeBehaviorOverride
            ?? _strategyPolicyEvaluator?.Invoke(new ContextPropagationStrategyPolicyContext
            {
                ContextType = typeof(TContext),
                Key = Key,
                PropertyType = typeof(TProperty),
                Direction = direction,
                PayloadSizeBytes = payloadSizeBytes
            })
            ?? _transportPolicy?.OversizeBehavior
            ?? ContextOversizeBehavior.FailFast;
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
