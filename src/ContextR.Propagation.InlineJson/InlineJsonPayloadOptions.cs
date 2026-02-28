namespace ContextR.Propagation.InlineJson;

/// <summary>
/// Options for inline JSON payload behavior.
/// </summary>
public sealed class InlineJsonPayloadOptions
{
    /// <summary>
    /// Maximum UTF-8 bytes per mapped property payload.
    /// Set to &lt;= 0 to disable size checks.
    /// </summary>
    public int MaxPayloadBytes { get; set; } = 4096;

    /// <summary>
    /// Oversize behavior for payloads exceeding <see cref="MaxPayloadBytes"/>.
    /// </summary>
    public ContextOversizeBehavior OversizeBehavior { get; set; } = ContextOversizeBehavior.FailFast;
}

internal sealed class InlineJsonTransportPolicy<TContext> : IContextTransportPolicy<TContext>
    where TContext : class
{
    public InlineJsonTransportPolicy(InlineJsonPayloadOptions options)
    {
        MaxPayloadBytes = options.MaxPayloadBytes;
        OversizeBehavior = options.OversizeBehavior;
    }

    public int MaxPayloadBytes { get; }

    public ContextOversizeBehavior OversizeBehavior { get; }
}
