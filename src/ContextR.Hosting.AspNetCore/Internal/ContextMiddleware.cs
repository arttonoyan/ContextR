using Microsoft.AspNetCore.Http;
using ContextR.Propagation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Hosting.AspNetCore.Internal;

internal sealed class ContextMiddleware<TContext> where TContext : class
{
    private readonly RequestDelegate _next;
    private readonly IPropagationExecutionScope _executionScope;
    private readonly string? _domain;

    public ContextMiddleware(RequestDelegate next, string? domain = null)
        : this(next, new AsyncLocalPropagationExecutionScope(), domain)
    { }

    public ContextMiddleware(
        RequestDelegate next,
        IPropagationExecutionScope executionScope,
        string? domain = null)
    {
        _next = next;
        _executionScope = executionScope;
        _domain = domain;
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        IContextPropagator<TContext> propagator,
        IContextWriter writer,
        IServiceProvider? serviceProvider = null,
        ContextRAspNetCoreOptionsRegistry<TContext>? optionsRegistry = null,
        ILogger<ContextMiddleware<TContext>>? logger = null)
    {
        using var _ = _executionScope.BeginDomainScope(_domain);
        serviceProvider ??= httpContext.RequestServices;
        optionsRegistry ??= serviceProvider.GetService<ContextRAspNetCoreOptionsRegistry<TContext>>();
        var options = optionsRegistry?.Resolve(serviceProvider, _domain) ?? new ContextRAspNetCoreOptions<TContext>();
        var enforcement = options.EnforcementOptions;
        TContext? context = null;
        Exception? extractionException = null;

        try
        {
            context = propagator.Extract(
                httpContext.Request.Headers,
                static (headers, key) => headers.TryGetValue(key, out var values) ? (string?)values : null);
        }
        catch (Exception ex)
        {
            extractionException = ex;
            logger?.LogWarning(ex, "ContextR extraction failed for context type {ContextType} in domain {Domain}.", typeof(TContext).FullName, _domain ?? "<default>");
        }

        if (extractionException is not null)
        {
            context = TryFallback(enforcement, httpContext, logger, out var fallbackException);
            if (context is null)
            {
                var failureReason = fallbackException is null
                    ? ContextIngressFailureReason.ExtractionFailed
                    : ContextIngressFailureReason.FallbackFailed;
                var decision = ResolveFailureDecision(enforcement, new ContextIngressFailureContext<TContext>
                {
                    HttpContext = httpContext,
                    Domain = _domain,
                    Reason = failureReason,
                    Exception = fallbackException ?? extractionException
                });

                if (await TryApplyFailureDecision(httpContext, decision))
                    return;
            }
        }

        if (context is not null)
        {
            if (_domain is not null)
                writer.SetContext(_domain, context);
            else
                writer.SetContext(context);
        }

        await _next(httpContext);
    }

    private static TContext? TryFallback(
        ContextRAspNetCoreEnforcementOptions<TContext> enforcement,
        HttpContext httpContext,
        ILogger<ContextMiddleware<TContext>>? logger,
        out Exception? exception)
    {
        exception = null;

        if (enforcement.FallbackContextFactory is null)
            return null;

        try
        {
            return enforcement.FallbackContextFactory(httpContext);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "ContextR fallback context factory failed for context type {ContextType}.", typeof(TContext).FullName);
            exception = ex;
            return null;
        }
    }

    private static ContextIngressFailureDecision ResolveFailureDecision(
        ContextRAspNetCoreEnforcementOptions<TContext> enforcement,
        ContextIngressFailureContext<TContext> failure)
    {
        if (enforcement.OnFailure is not null)
            return enforcement.OnFailure(failure);

        return enforcement.Mode switch
        {
            ContextIngressEnforcementMode.Disabled => ContextIngressFailureDecision.Continue(),
            ContextIngressEnforcementMode.ObserveOnly => ContextIngressFailureDecision.Continue(),
            ContextIngressEnforcementMode.FailRequest => ContextIngressFailureDecision.Fail(
                StatusCodes.Status400BadRequest,
                "Request context validation failed."),
            _ => ContextIngressFailureDecision.Continue()
        };
    }

    private static async Task<bool> TryApplyFailureDecision(HttpContext httpContext, ContextIngressFailureDecision decision)
    {
        if (!decision.ShouldFailRequest)
            return false;

        if (httpContext.Response.HasStarted)
            return true;

        if (decision.ResponseWriter is not null)
        {
            await decision.ResponseWriter(httpContext);
            return true;
        }

        httpContext.Response.StatusCode = decision.StatusCode;
        if (!string.IsNullOrWhiteSpace(decision.Message))
            await httpContext.Response.WriteAsync(decision.Message);

        return true;
    }
}
