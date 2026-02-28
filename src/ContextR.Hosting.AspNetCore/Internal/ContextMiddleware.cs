using Microsoft.AspNetCore.Http;
using ContextR.Propagation.Abstractions;

namespace ContextR.Hosting.AspNetCore.Internal;

internal sealed class ContextMiddleware<TContext> where TContext : class
{
    private readonly RequestDelegate _next;
    private readonly string? _domain;

    public ContextMiddleware(RequestDelegate next, string? domain = null)
    {
        _next = next;
        _domain = domain;
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        IContextPropagator<TContext> propagator,
        IContextWriter writer)
    {
        var context = propagator.Extract(
            httpContext.Request.Headers,
            static (headers, key) => headers.TryGetValue(key, out var values) ? (string?)values : null);

        if (context is not null)
        {
            if (_domain is not null)
                writer.SetContext(_domain, context);
            else
                writer.SetContext(context);
        }

        await _next(httpContext);
    }
}
