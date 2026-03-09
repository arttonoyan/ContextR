using ContextR.OpenFeature;

namespace ContextR.OpenFeature.UnitTests;

/// <summary>
/// Verifies that the cycle detection in TryConvert correctly detects self-referencing
/// and indirectly-referencing dictionaries and throws a descriptive exception instead
/// of recursing infinitely.
/// </summary>
public sealed class CircularReferenceTests
{
    [Fact]
    public void Apply_WithCircularDictionaryReference_ThrowsDescriptiveException()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<DictContext>());

        services.AddOpenFeature(featureBuilder =>
        {
            featureBuilder.UseContextR(options =>
            {
                options.Map<DictContext>(map => map.MapProperty(x => x.Data, "data"));
            });
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var circular = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = "test"
        };
        circular["self"] = circular;

        scope.ServiceProvider.GetRequiredService<IContextWriter>()
            .SetContext(new DictContext(circular));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<EvaluationContext>());

        Assert.Contains("Circular reference", ex.Message);
    }

    [Fact]
    public void Apply_WithIndirectCircularReference_ThrowsDescriptiveException()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<DictContext>());

        services.AddOpenFeature(featureBuilder =>
        {
            featureBuilder.UseContextR(options =>
            {
                options.Map<DictContext>(map => map.MapProperty(x => x.Data, "data"));
            });
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var dictA = new Dictionary<string, object?>(StringComparer.Ordinal);
        var dictB = new Dictionary<string, object?>(StringComparer.Ordinal);
        dictA["child"] = dictB;
        dictB["parent"] = dictA;

        scope.ServiceProvider.GetRequiredService<IContextWriter>()
            .SetContext(new DictContext(dictA));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<EvaluationContext>());

        Assert.Contains("Circular reference", ex.Message);
    }

    [Fact]
    public void Apply_WithNestedDictionary_NoCircularReference_Succeeds()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<DictContext>());

        services.AddOpenFeature(featureBuilder =>
        {
            featureBuilder.UseContextR(options =>
            {
                options.Map<DictContext>(map => map.MapProperty(x => x.Data, "data"));
            });
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var nested = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["inner"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["value"] = "hello"
            }
        };

        scope.ServiceProvider.GetRequiredService<IContextWriter>()
            .SetContext(new DictContext(nested));

        var evalCtx = scope.ServiceProvider.GetRequiredService<EvaluationContext>();
        Assert.NotNull(evalCtx);
    }

    private sealed record DictContext(IDictionary<string, object?> Data);
}
