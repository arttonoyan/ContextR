using Microsoft.AspNetCore.Http;

namespace ContextR.AspNetCore.Internal;

internal sealed class ContextMiddleware<TContext> where TContext : class
{
    private readonly RequestDelegate _next;

    public ContextMiddleware(RequestDelegate next)
    {
        _next = next;
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
            writer.SetContext(context);

        await _next(httpContext);
    }
}
