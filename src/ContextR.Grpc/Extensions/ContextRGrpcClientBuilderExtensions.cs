using ContextR.Grpc;
using ContextR;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding context propagation to gRPC client registrations.
/// </summary>
public static class ContextRGrpcClientBuilderExtensions
{
    /// <summary>
    /// Adds a <see cref="ContextPropagationInterceptor{TContext}"/> to this gRPC client's pipeline,
    /// propagating context to outgoing gRPC calls.
    /// </summary>
    /// <typeparam name="TContext">The context type to propagate.</typeparam>
    /// <param name="builder">The HTTP client builder returned by <c>AddGrpcClient</c>.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IHttpClientBuilder AddContextRGrpcPropagation<TContext>(this IHttpClientBuilder builder)
        where TContext : class
    {
        builder.Services.TryAddTransient<ContextPropagationInterceptor<TContext>>();
        return builder.AddInterceptor<ContextPropagationInterceptor<TContext>>();
    }

    /// <summary>
    /// Adds a ContextR propagation interceptor registration to gRPC client factory options.
    /// </summary>
    /// <typeparam name="TContext">The context type to propagate.</typeparam>
    /// <param name="options">The gRPC client factory options.</param>
    /// <param name="domain">Optional domain to read context from.</param>
    public static void AddContextRPropagationInterceptor<TContext>(
        this GrpcClientFactoryOptions options,
        string? domain = null)
        where TContext : class
    {
        options.InterceptorRegistrations.Add(new InterceptorRegistration(
            InterceptorScope.Client,
            sp => new ContextPropagationInterceptor<TContext>(
                sp.GetRequiredService<IContextAccessor>(),
                sp.GetRequiredService<IContextPropagator<TContext>>(),
                domain)));
    }
}
