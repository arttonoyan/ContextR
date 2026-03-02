using ContextR.Hosting.AspNetCore.Internal;
using ContextR.Propagation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContextR.Hosting.AspNetCore;

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
        return UseAspNetCore(builder, _ => { });
    }

    /// <summary>
    /// Registers ASP.NET Core extraction middleware and configures transport/enforcement behavior.
    /// </summary>
    /// <typeparam name="TContext">The context type to extract.</typeparam>
    /// <param name="builder">The context registration builder.</param>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IContextRegistrationBuilder<TContext> UseAspNetCore<TContext>(
        this IContextRegistrationBuilder<TContext> builder,
        Action<ContextRAspNetCoreOptions<TContext>> configure)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        return UseAspNetCore(
            builder,
            _ =>
            {
                var options = new ContextRAspNetCoreOptions<TContext>();
                configure(options);
                return options;
            });
    }

    /// <summary>
    /// Registers ASP.NET Core extraction middleware and configures options using DI + mutable options callback.
    /// </summary>
    /// <typeparam name="TContext">The context type to extract.</typeparam>
    /// <param name="builder">The context registration builder.</param>
    /// <param name="configure">
    /// Callback that receives <see cref="IServiceProvider"/> and mutable options instance.
    /// </param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IContextRegistrationBuilder<TContext> UseAspNetCore<TContext>(
        this IContextRegistrationBuilder<TContext> builder,
        Action<IServiceProvider, ContextRAspNetCoreOptions<TContext>> configure)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        return UseAspNetCore(
            builder,
            sp =>
            {
                var options = new ContextRAspNetCoreOptions<TContext>();
                configure(sp, options);
                return options;
            });
    }

    /// <summary>
    /// Registers ASP.NET Core extraction middleware with options configured from DI.
    /// </summary>
    /// <typeparam name="TContext">The context type to extract.</typeparam>
    /// <param name="builder">The context registration builder.</param>
    /// <param name="configureFactory">
    /// Factory that builds options using an <see cref="IServiceProvider"/> (for logger/services access).
    /// </param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IContextRegistrationBuilder<TContext> UseAspNetCore<TContext>(
        this IContextRegistrationBuilder<TContext> builder,
        Func<IServiceProvider, ContextRAspNetCoreOptions<TContext>> configureFactory)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureFactory);

        var domain = builder.Domain;
        builder.Services.TryAddSingleton<IPropagationExecutionScope, AsyncLocalPropagationExecutionScope>();
        var optionsRegistry = GetOrAddAspNetCoreOptionsRegistry<TContext>(builder.Services);
        optionsRegistry.TryAdd(domain, configureFactory);

        builder.Services.AddSingleton<IStartupFilter>(
            _ => new ContextStartupFilter<TContext>(domain));

        return builder;
    }

    private static ContextRAspNetCoreOptionsRegistry<TContext> GetOrAddAspNetCoreOptionsRegistry<TContext>(
        IServiceCollection services)
        where TContext : class
    {
        var existing = services
            .FirstOrDefault(d => d.ServiceType == typeof(ContextRAspNetCoreOptionsRegistry<TContext>))
            ?.ImplementationInstance as ContextRAspNetCoreOptionsRegistry<TContext>;

        if (existing is not null)
            return existing;

        var created = new ContextRAspNetCoreOptionsRegistry<TContext>();
        services.AddSingleton(created);
        return created;
    }
}
