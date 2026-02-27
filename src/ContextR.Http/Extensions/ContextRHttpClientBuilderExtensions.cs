using ContextR.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding context propagation to specific <see cref="IHttpClientBuilder"/> instances.
/// </summary>
public static class ContextRHttpClientBuilderExtensions
{
    /// <summary>
    /// Adds a <see cref="ContextPropagationHandler{TContext}"/> to this HttpClient's pipeline,
    /// propagating context to outgoing requests.
    /// <para>
    /// Use this for per-client propagation. For global propagation to all HttpClients,
    /// use <c>.UseGlobalHttpPropagation()</c> inside the <c>AddContextR</c> builder instead.
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">The context type to propagate.</typeparam>
    /// <param name="builder">The HTTP client builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IHttpClientBuilder AddContextRHandler<TContext>(this IHttpClientBuilder builder)
        where TContext : class
    {
        builder.Services.TryAddScoped<ContextPropagationHandler<TContext>>();
        return builder.AddHttpMessageHandler<ContextPropagationHandler<TContext>>();
    }
}
