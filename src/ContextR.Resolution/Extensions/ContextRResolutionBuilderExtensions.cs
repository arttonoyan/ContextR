namespace ContextR.Resolution;

/// <summary>
/// Builder-level extensions for enabling resolution services from inside <c>AddContextR(...)</c>.
/// </summary>
public static class ContextRResolutionBuilderExtensions
{
    /// <summary>
    /// Enables ContextR resolution services for this registration graph.
    /// </summary>
    /// <param name="builder">The ContextR builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IContextBuilder UseResolution(this IContextBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddContextRResolution();
        return builder;
    }
}
