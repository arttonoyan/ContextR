namespace ContextR.Propagation;

/// <summary>
/// Defines how mapped property values are serialized and deserialized
/// for a specific context type.
/// </summary>
/// <typeparam name="TContext">The context type being configured.</typeparam>
public interface IContextPayloadSerializer<TContext>
    where TContext : class
{
    /// <summary>
    /// Returns whether this serializer should handle the given property type.
    /// </summary>
    bool CanHandle(Type propertyType);

    /// <summary>
    /// Serializes a mapped property value.
    /// </summary>
    string Serialize(object value, Type propertyType);

    /// <summary>
    /// Attempts to deserialize a mapped property value.
    /// </summary>
    bool TryDeserialize(string payload, Type propertyType, out object? value);
}

/// <summary>
/// Oversize behavior for serialized payload values.
/// </summary>
public enum ContextOversizeBehavior
{
    /// <summary>
    /// Throw a deterministic exception when payload exceeds configured limit.
    /// </summary>
    FailFast = 0,

    /// <summary>
    /// Skip this mapped property when payload exceeds configured limit.
    /// </summary>
    SkipProperty = 1,

    /// <summary>
    /// Indicates a token/reference fallback should be used.
    /// If no token strategy is registered, behavior should fail deterministically.
    /// </summary>
    FallbackToToken = 2,

    /// <summary>
    /// Split oversized payload into multiple chunks under derived keys.
    /// </summary>
    ChunkProperty = 3
}

/// <summary>
/// Defines max payload constraints and oversize behavior for mapped properties
/// for a specific context type.
/// </summary>
/// <typeparam name="TContext">The context type being configured.</typeparam>
public interface IContextTransportPolicy<TContext>
    where TContext : class
{
    /// <summary>
    /// Maximum payload size in UTF-8 bytes for a single mapped property value.
    /// Values less than or equal to zero disable size checks.
    /// </summary>
    int MaxPayloadBytes { get; }

    /// <summary>
    /// Action to take when <see cref="MaxPayloadBytes"/> is exceeded.
    /// </summary>
    ContextOversizeBehavior OversizeBehavior { get; }
}
