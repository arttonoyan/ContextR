using ContextR.Propagation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContextR.Transport.Http;

/// <summary>
/// Extension methods for configuring HTTP context propagation on <see cref="IContextRegistrationBuilder{TContext}"/>.
/// </summary>
public static class ContextRHttpRegistrationExtensions
{
    /// <summary>
    /// Registers a <see cref="ContextPropagationHandler{TContext}"/> that propagates context
    /// to <b>all</b> outgoing <see cref="HttpClient"/> requests created by the <c>IHttpClientFactory</c>.
    /// <para>
    /// When used within <see cref="IContextBuilder.AddDomain"/>, context is read
    /// from the specified domain rather than the default.
    /// </para>
    /// <para>
    /// For per-client control, use <see cref="ContextRHttpClientBuilderExtensions.AddContextRHandler{TContext}"/>
    /// on an <see cref="IHttpClientBuilder"/> instead.
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">The context type to propagate.</typeparam>
    /// <param name="builder">The context registration builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IContextRegistrationBuilder<TContext> UseGlobalHttpPropagation<TContext>(
        this IContextRegistrationBuilder<TContext> builder)
        where TContext : class
    {
        var domain = builder.Domain;
        builder.Services.TryAddSingleton<IPropagationExecutionScope, AsyncLocalPropagationExecutionScope>();

        builder.Services.TryAddScoped(sp => new ContextPropagationHandler<TContext>(
            sp.GetRequiredService<IContextAccessor>(),
            sp.GetRequiredService<IContextPropagator<TContext>>(),
            domain,
            sp.GetRequiredService<IPropagationExecutionScope>()));

        builder.Services.ConfigureHttpClientDefaults(http =>
            http.AddHttpMessageHandler<ContextPropagationHandler<TContext>>());

        return builder;
    }
}
