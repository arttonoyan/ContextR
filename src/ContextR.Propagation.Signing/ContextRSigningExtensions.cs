using ContextR.Propagation.Signing.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContextR.Propagation.Signing;

/// <summary>
/// Extension methods for registering HMAC-based context signing.
/// </summary>
public static class ContextRSigningExtensions
{
    /// <summary>
    /// Registers HMAC-based signing for context propagation.
    /// Provide keys inline via <see cref="SigningOptions.Key"/> or <see cref="SigningOptions.AddKey"/>,
    /// or register an <see cref="ISigningKeyProvider"/> in DI and set <see cref="SigningOptions.KeyId"/>.
    /// </summary>
    public static IContextRegistrationBuilder<TContext> UseContextSigning<TContext>(
        this IContextRegistrationBuilder<TContext> builder,
        Action<SigningOptions> configure)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new SigningOptions();
        configure(options);

        if (!options.HasInlineKeys && string.IsNullOrWhiteSpace(options.KeyId))
        {
            throw new ArgumentException(
                "Either set SigningOptions.Key / AddKey() for inline keys, " +
                "or set SigningOptions.KeyId with a registered ISigningKeyProvider.",
                nameof(configure));
        }

        if (options.HasInlineKeys)
            options.KeyId ??= "__inline__";

        builder.Services.TryAddSingleton<IPropagationExecutionScope, AsyncLocalPropagationExecutionScope>();

        var descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(IContextPropagator<TContext>));

        if (descriptor is not null)
        {
            builder.Services.Remove(descriptor);

            builder.Services.AddSingleton<IContextPropagator<TContext>>(sp =>
            {
                var inner = CreateInnerPropagator<TContext>(descriptor, sp);
                return CreateSigningPropagator(sp, inner, options);
            });
        }
        else
        {
            builder.Services.AddSingleton<IContextPropagator<TContext>>(sp =>
            {
                var inner = sp.GetRequiredService<IContextPropagator<TContext>>();
                return CreateSigningPropagator(sp, inner, options);
            });
        }

        return builder;
    }

    private static SigningContextPropagator<TContext> CreateSigningPropagator<TContext>(
        IServiceProvider sp,
        IContextPropagator<TContext> inner,
        SigningOptions options)
        where TContext : class
    {
        var keyProvider = options.HasInlineKeys
            ? new InMemorySigningKeyProvider(options.InlineKeys, options.CurrentKeyVersion)
            : sp.GetRequiredService<ISigningKeyProvider>();

        return new SigningContextPropagator<TContext>(
            inner,
            keyProvider,
            options,
            sp,
            sp.GetService<IPropagationExecutionScope>(),
            sp.GetService<ContextPropagationFailureHandlerRegistry<TContext>>());
    }

    private static IContextPropagator<TContext> CreateInnerPropagator<TContext>(
        ServiceDescriptor descriptor,
        IServiceProvider sp)
        where TContext : class
    {
        if (descriptor.ImplementationInstance is IContextPropagator<TContext> instance)
            return instance;

        if (descriptor.ImplementationFactory is not null)
            return (IContextPropagator<TContext>)descriptor.ImplementationFactory(sp);

        if (descriptor.ImplementationType is not null)
            return (IContextPropagator<TContext>)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);

        throw new InvalidOperationException(
            $"Cannot resolve inner IContextPropagator<{typeof(TContext).Name}> from existing service descriptor.");
    }
}
