using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Propagation.UnitTests;

public sealed class MappingDslTests
{
    [Fact]
    public void Map_RequiredAndOptionalProperties_AreAppliedDuringInjectionAndExtraction()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .Map(m => m
                    .Property(c => c.TraceId, "X-Trace-Id").Required()
                    .Property(c => c.Payload, "X-Payload").Optional()));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var injectCarrier = new Dictionary<string, string>();
        propagator.Inject(new TestContext { TraceId = "trace-1", Payload = null }, injectCarrier, static (c, k, v) => c[k] = v);

        Assert.Equal("trace-1", injectCarrier["X-Trace-Id"]);
        Assert.False(injectCarrier.ContainsKey("X-Payload"));

        var extractCarrier = new Dictionary<string, string>
        {
            ["X-Trace-Id"] = "trace-2"
        };

        var extracted = propagator.Extract(extractCarrier, static (c, k) => c.TryGetValue(k, out var v) ? v : null);
        Assert.NotNull(extracted);
        Assert.Equal("trace-2", extracted.TraceId);
        Assert.Null(extracted.Payload);
    }

    [Fact]
    public void Map_RequiredPropertyMissingOnInject_Throws()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .Map(m => m.Property(c => c.TraceId, "X-Trace-Id").Required()));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var ex = Assert.ThrowsAny<InvalidOperationException>(() =>
            propagator.Inject(new TestContext(), new Dictionary<string, string>(), static (c, k, v) => c[k] = v));

        Assert.Contains("reason 'MissingRequired'", ex.Message);
    }

    [Fact]
    public void Map_RequiredPropertyMissingOnExtract_Throws()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .Map(m => m.Property(c => c.TraceId, "X-Trace-Id").Required()));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var ex = Assert.ThrowsAny<InvalidOperationException>(() =>
            propagator.Extract(new Dictionary<string, string>(), static (c, k) => c.TryGetValue(k, out var v) ? v : null));

        Assert.Contains("reason 'MissingRequired'", ex.Message);
    }

    [Fact]
    public void Map_RequiredPropertyInvalidOnExtract_Throws()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<IntContext>(reg => reg
                .Map(m => m.Property(c => c.Count, "X-Count").Required()));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<IntContext>>();

        var carrier = new Dictionary<string, string> { ["X-Count"] = "not-an-int" };
        var ex = Assert.ThrowsAny<InvalidOperationException>(() =>
            propagator.Extract(carrier, static (c, k) => c.TryGetValue(k, out var v) ? v : null));

        Assert.Contains("reason 'ParseFailed'", ex.Message);
    }

    [Fact]
    public void Map_Throws_WhenConfigureIsNull()
    {
        var services = new ServiceCollection();
        IContextRegistrationBuilder<TestContext>? captured = null;

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => captured = reg);
        });

        Assert.NotNull(captured);
        Assert.Throws<ArgumentNullException>(() =>
            ContextRPropagationExtensions.Map(captured!, (Func<ContextMapBuilder<TestContext>, ContextMapBuilder<TestContext>>)null!));
    }

    [Fact]
    public void OnPropagationFailure_AllowsSkippingRequiredFailure()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .OnPropagationFailure<TestContext>(failure =>
                {
                    Assert.Equal(PropagationFailureReason.MissingRequired, failure.Reason);
                    Assert.Equal(PropagationDirection.Extract, failure.Direction);
                    Assert.Equal("X-Trace-Id", failure.Key);
                    return PropagationFailureAction.SkipProperty;
                })
                .Map(m => m.Property(c => c.TraceId, "X-Trace-Id").Required()));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var extracted = propagator.Extract(new Dictionary<string, string>(), static (c, k) => c.TryGetValue(k, out var v) ? v : null);
        Assert.Null(extracted);
    }

    [Fact]
    public void OnPropagationFailure_CanSkipEntireContext()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .OnPropagationFailure<TestContext>(_ => PropagationFailureAction.SkipContext)
                .Map(m => m
                    .Property(c => c.TraceId, "X-Trace-Id").Required()
                    .Property(c => c.Payload, "X-Payload").Optional()));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var extracted = propagator.Extract(new Dictionary<string, string>(), static (c, k) => c.TryGetValue(k, out var v) ? v : null);
        Assert.Null(extracted);
    }

    [Fact]
    public void OnPropagationFailure_UsesDomainSpecificHandler_WhenDomainScopeIsSet()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .OnPropagationFailure(_ => PropagationFailureAction.Throw)
                .Map(m => m.Property(c => c.TraceId, "X-Trace-Id").Required()));

            builder.AddDomain("web-api", domain =>
            {
                domain.Add<TestContext>(reg => reg
                    .OnPropagationFailure(_ => PropagationFailureAction.SkipProperty));
            });
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();
        var executionScope = provider.GetRequiredService<IPropagationExecutionScope>();

        using var _ = executionScope.BeginDomainScope("web-api");
        var extracted = propagator.Extract(new Dictionary<string, string>(), static (c, k) => c.TryGetValue(k, out var v) ? v : null);
        Assert.Null(extracted);
    }

    [Fact]
    public void OnPropagationFailure_FallsBackToDefaultHandler_WhenDomainSpecificMissing()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .OnPropagationFailure<TestContext>(_ => PropagationFailureAction.SkipProperty)
                .Map(m => m.Property(c => c.TraceId, "X-Trace-Id").Required()));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();
        var executionScope = provider.GetRequiredService<IPropagationExecutionScope>();

        using var _ = executionScope.BeginDomainScope("non-existing");
        var extracted = propagator.Extract(new Dictionary<string, string>(), static (c, k) => c.TryGetValue(k, out var v) ? v : null);
        Assert.Null(extracted);
    }

    private sealed class TestContext
    {
        public string? TraceId { get; set; }
        public string? Payload { get; set; }
    }

    private sealed class IntContext
    {
        public int Count { get; set; }
    }
}
