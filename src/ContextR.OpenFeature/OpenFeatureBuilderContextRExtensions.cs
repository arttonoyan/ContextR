using ContextR.OpenFeature;
using ContextR.OpenFeature.Internal;
using OpenFeature.Hosting;

namespace OpenFeature;

/// <summary>
/// ContextR integration extensions for OpenFeature.Hosting.
/// </summary>
public static class OpenFeatureBuilderContextRExtensions
{
    /// <summary>
    /// Enables ContextR-backed OpenFeature EvaluationContext with default options.
    /// </summary>
    public static OpenFeatureBuilder UseContextR(this OpenFeatureBuilder builder)
    {
        return UseContextR(builder, static _ => { });
    }

    /// <summary>
    /// Enables ContextR-backed OpenFeature EvaluationContext with custom options.
    /// </summary>
    public static OpenFeatureBuilder UseContextR(
        this OpenFeatureBuilder builder,
        Action<ContextROpenFeatureOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ContextROpenFeatureOptions();
        configure(options);

        builder.AddContext((contextBuilder, serviceProvider) =>
            ContextREvaluationContextApplier.Apply(contextBuilder, serviceProvider, options));

        return builder;
    }

    /// <summary>
    /// Enables ContextR-backed OpenFeature EvaluationContext for a primary context type.
    /// </summary>
    public static OpenFeatureBuilder UseContextR<TContext>(
        this OpenFeatureBuilder builder,
        Action<ContextROpenFeatureOptions<TContext>> configure)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ContextROpenFeatureOptions<TContext>();
        configure(options);
        builder.AddContext((contextBuilder, serviceProvider) =>
            ContextREvaluationContextApplier.Apply(contextBuilder, serviceProvider, options));
        return builder;
    }
}
