using ContextR.Propagation;
using ContextR.Transport.Grpc;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContextR.Transport.Grpc;

/// <summary>
/// Extension methods for configuring gRPC context propagation
/// on <see cref="IContextRegistrationBuilder{TContext}"/>.
/// </summary>
public static class ContextRGrpcRegistrationExtensions
{
    /// <summary>
    /// Registers a <see cref="ContextPropagationInterceptor{TContext}"/> that propagates context
    /// to <b>all</b> outgoing gRPC clients configured with <c>AddGrpcClient</c>.
    /// <para>
    /// When used within <see cref="IContextBuilder.AddDomain"/>, context is read
    /// from the specified domain rather than the default.
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">The context type to propagate.</typeparam>
    /// <param name="builder">The context registration builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IContextRegistrationBuilder<TContext> UseGlobalGrpcPropagation<TContext>(
        this IContextRegistrationBuilder<TContext> builder)
        where TContext : class
    {
        var domain = builder.Domain;

        builder.Services.TryAddTransient(sp => new ContextPropagationInterceptor<TContext>(
            sp.GetRequiredService<IContextAccessor>(),
            sp.GetRequiredService<IContextPropagator<TContext>>(),
            domain));

        builder.Services.ConfigureAll<GrpcClientFactoryOptions>(options =>
            options.AddContextRPropagationInterceptor<TContext>(domain));

        return builder;
    }
}
