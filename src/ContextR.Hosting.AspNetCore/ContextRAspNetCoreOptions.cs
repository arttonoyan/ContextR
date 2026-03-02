using Microsoft.AspNetCore.Http;

namespace ContextR.Hosting.AspNetCore;

/// <summary>
/// Ingress enforcement behavior for ASP.NET Core extraction middleware.
/// </summary>
public enum ContextIngressEnforcementMode
{
    /// <summary>
    /// Enforcement is disabled; middleware behavior stays extraction-only.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// Enforcement issues are observed (callback/logging) but request continues.
    /// </summary>
    ObserveOnly = 1,

    /// <summary>
    /// Enforcement issues fail the request unless custom handler decides otherwise.
    /// </summary>
    FailRequest = 2
}

/// <summary>
/// Failure reason for ingress enforcement.
/// </summary>
public enum ContextIngressFailureReason
{
    /// <summary>
    /// Context extraction threw an exception (typically missing required/parse failure).
    /// </summary>
    ExtractionFailed = 0,

    /// <summary>
    /// Fallback context factory threw an exception.
    /// </summary>
    FallbackFailed = 1
}

/// <summary>
/// Decision returned by ingress failure handlers.
/// </summary>
public sealed class ContextIngressFailureDecision
{
    private ContextIngressFailureDecision()
    {
    }

    /// <summary>
    /// Indicates whether request should fail.
    /// </summary>
    public bool ShouldFailRequest { get; private init; }

    /// <summary>
    /// Response status code when <see cref="ShouldFailRequest"/> is true.
    /// </summary>
    public int StatusCode { get; private init; } = StatusCodes.Status400BadRequest;

    /// <summary>
    /// Optional plain-text message when no custom writer is provided.
    /// </summary>
    public string? Message { get; private init; }

    /// <summary>
    /// Optional custom response writer.
    /// </summary>
    public Func<HttpContext, Task>? ResponseWriter { get; private init; }

    /// <summary>
    /// Continue request pipeline.
    /// </summary>
    public static ContextIngressFailureDecision Continue() => new();

    /// <summary>
    /// Fail request with status/message.
    /// </summary>
    public static ContextIngressFailureDecision Fail(int statusCode = StatusCodes.Status400BadRequest, string? message = null)
        => new()
        {
            ShouldFailRequest = true,
            StatusCode = statusCode,
            Message = message
        };

    /// <summary>
    /// Fail request using a custom writer.
    /// </summary>
    public static ContextIngressFailureDecision FailWithWriter(Func<HttpContext, Task> responseWriter)
    {
        ArgumentNullException.ThrowIfNull(responseWriter);
        return new ContextIngressFailureDecision
        {
            ShouldFailRequest = true,
            ResponseWriter = responseWriter
        };
    }
}

/// <summary>
/// Failure context passed to enforcement callbacks.
/// </summary>
public sealed class ContextIngressFailureContext<TContext>
    where TContext : class
{
    /// <summary>
    /// The HTTP context for the current request.
    /// </summary>
    public required HttpContext HttpContext { get; init; }

    /// <summary>
    /// The context type under extraction.
    /// </summary>
    public Type ContextType { get; init; } = typeof(TContext);

    /// <summary>
    /// Domain currently in effect for extraction.
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// Failure reason.
    /// </summary>
    public ContextIngressFailureReason Reason { get; init; }

    /// <summary>
    /// Optional underlying exception.
    /// </summary>
    public Exception? Exception { get; init; }
}

/// <summary>
/// Enforcement options for ASP.NET Core middleware.
/// </summary>
public sealed class ContextRAspNetCoreEnforcementOptions<TContext>
    where TContext : class
{
    /// <summary>
    /// Enforcement mode. Defaults to <see cref="ContextIngressEnforcementMode.Disabled"/>.
    /// </summary>
    public ContextIngressEnforcementMode Mode { get; set; } = ContextIngressEnforcementMode.Disabled;

    /// <summary>
    /// Optional callback to customize decision per failure.
    /// </summary>
    public Func<ContextIngressFailureContext<TContext>, ContextIngressFailureDecision>? OnFailure { get; set; }

    /// <summary>
    /// Optional fallback factory used when extraction or enforcement fails.
    /// </summary>
    public Func<HttpContext, TContext?>? FallbackContextFactory { get; set; }
}

/// <summary>
/// ASP.NET Core transport options for a registered context type.
/// </summary>
public sealed class ContextRAspNetCoreOptions<TContext>
    where TContext : class
{
    internal ContextRAspNetCoreEnforcementOptions<TContext> EnforcementOptions { get; } = new();

    /// <summary>
    /// Configures ingress enforcement behavior.
    /// </summary>
    public ContextRAspNetCoreOptions<TContext> Enforcement(Action<ContextRAspNetCoreEnforcementOptions<TContext>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(EnforcementOptions);
        return this;
    }
}
