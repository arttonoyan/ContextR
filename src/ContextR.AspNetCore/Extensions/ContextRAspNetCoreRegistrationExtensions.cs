using ContextR.AspNetCore.Internal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR;

/// <summary>
/// Extension methods for configuring ASP.NET Core context extraction
/// on <see cref="IContextRegistrationBuilder{TContext}"/>.
/// </summary>
public static class ContextRAspNetCoreRegistrationExtensions
{
    /// <summary>
    /// Registers middleware that automatically extracts context from incoming HTTP request headers
    /// using the registered <see cref="IContextPropagator{TContext}"/>.
    /// The middleware is automatically added to the beginning of the request pipeline
    /// via an <see cref="IStartupFilter"/>.
    /// <para>
    /// When used within <see cref="IContextBuilder.AddDomain"/>, context is written
    /// to the specified domain rather than the default.
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">The context type to extract.</typeparam>
    /// <param name="builder">The context registration builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IContextRegistrationBuilder<TContext> UseAspNetCore<TContext>(
        this IContextRegistrationBuilder<TContext> builder)
        where TContext : class
    {
        var domain = builder.Domain;
        builder.Services.AddSingleton<IStartupFilter>(
            _ => new ContextStartupFilter<TContext>(domain));

        return builder;
    }
}
