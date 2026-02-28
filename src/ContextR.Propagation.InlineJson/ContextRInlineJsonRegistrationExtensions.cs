using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContextR.Propagation.InlineJson;

/// <summary>
/// Registration extensions for inline JSON payload strategy.
/// </summary>
public static class ContextRInlineJsonRegistrationExtensions
{
    /// <summary>
    /// Registers inline JSON serializer and default strict payload policy.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UseInlineJsonPayloads<TContext>(
        this IContextRegistrationBuilder<TContext> builder)
        where TContext : class
    {
        return builder.UseInlineJsonPayloads<TContext>(_ => { });
    }

    /// <summary>
    /// Registers inline JSON serializer and payload policy with custom options.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UseInlineJsonPayloads<TContext>(
        this IContextRegistrationBuilder<TContext> builder,
        Action<InlineJsonPayloadOptions> configure)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new InlineJsonPayloadOptions();
        configure(options);

        builder.Services.TryAddSingleton<IContextPayloadSerializer<TContext>, InlineJsonPayloadSerializer<TContext>>();
        builder.Services.TryAddSingleton<IContextTransportPolicy<TContext>>(
            _ => new InlineJsonTransportPolicy<TContext>(options));

        return builder;
    }
}
