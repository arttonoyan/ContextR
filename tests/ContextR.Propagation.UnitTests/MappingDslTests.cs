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
        IContextTypeBuilder<TestContext>? captured = null;

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => captured = reg);
        });

        Assert.NotNull(captured);
        Assert.Throws<ArgumentNullException>(() =>
            ContextRPropagationExtensions.Map(captured!, null!));
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

    [Fact]
    public void UseNullabilityConventions_MapProperty_InfersRequiredForNonNullableProperty()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<ConventionContext>(reg => reg
                .UseNullabilityConventions()
                .MapProperty(c => c.TenantId, "X-Tenant-Id"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<ConventionContext>>();

        var ex = Assert.ThrowsAny<InvalidOperationException>(() =>
            propagator.Extract(new Dictionary<string, string>(), static (c, k) => c.TryGetValue(k, out var v) ? v : null));

        Assert.Contains("reason 'MissingRequired'", ex.Message);
    }

    [Fact]
    public void UseNullabilityConventions_MapProperty_InfersOptionalForNullableProperty()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<ConventionContext>(reg => reg
                .UseNullabilityConventions()
                .MapProperty(c => c.UserId, "X-User-Id"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<ConventionContext>>();

        var extracted = propagator.Extract(new Dictionary<string, string>(), static (c, k) => c.TryGetValue(k, out var v) ? v : null);
        Assert.Null(extracted);
    }

    [Fact]
    public void UseNullabilityConventions_ByConvention_InfersRequiredForNonNullableProperty()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<ConventionContext>(reg => reg
                .UseNullabilityConventions()
                .Map(m => m
                    .Property(c => c.TenantId, "X-Tenant-Id").ByConvention()));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<ConventionContext>>();

        var ex = Assert.ThrowsAny<InvalidOperationException>(() =>
            propagator.Extract(new Dictionary<string, string>(), static (c, k) => c.TryGetValue(k, out var v) ? v : null));

        Assert.Contains("reason 'MissingRequired'", ex.Message);
    }

    [Fact]
    public void MapProperty_DefaultConventions_InfersRequiredForNonNullableProperty()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<ConventionContext>(reg => reg
                .MapProperty(c => c.TenantId, "X-Tenant-Id"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<ConventionContext>>();

        var ex = Assert.ThrowsAny<InvalidOperationException>(() =>
            propagator.Extract(new Dictionary<string, string>(), static (c, k) => c.TryGetValue(k, out var v) ? v : null));

        Assert.Contains("reason 'MissingRequired'", ex.Message);
    }

    [Fact]
    public void DisableNullabilityConventions_MapProperty_TreatsNonNullableAsOptional()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<ConventionContext>(reg => reg
                .DisableNullabilityConventions()
                .MapProperty(c => c.TenantId, "X-Tenant-Id"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<ConventionContext>>();

        var extracted = propagator.Extract(new Dictionary<string, string>(), static (c, k) => c.TryGetValue(k, out var v) ? v : null);
        Assert.Null(extracted);
    }

    [Fact]
    public void Map_ByConvention_AppliesConventionToPropertiesWithoutTerminalCalls()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<ConventionContext>(reg => reg
                .Map(m => m
                    .ByConvention()
                    .Property(c => c.TenantId, "X-Tenant-Id")
                    .Property(c => c.UserId, "X-User-Id")));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<ConventionContext>>();

        var ex = Assert.ThrowsAny<InvalidOperationException>(() =>
            propagator.Extract(new Dictionary<string, string>(), static (c, k) => c.TryGetValue(k, out var v) ? v : null));

        Assert.Contains("reason 'MissingRequired'", ex.Message);
    }

    [Fact]
    public void Map_DefaultBehavior_WithoutTerminalCall_UsesOptional()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<ConventionContext>(reg => reg
                .DisableNullabilityConventions()
                .Map(m => m
                    .Property(c => c.TenantId, "X-Tenant-Id")));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<ConventionContext>>();

        var extracted = propagator.Extract(new Dictionary<string, string>(), static (c, k) => c.TryGetValue(k, out var v) ? v : null);
        Assert.Null(extracted);
    }

    [Fact]
    public void Map_Property_Throws_WhenExpressionIsNull()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddContextR(builder =>
                builder.Add<ConventionContext>(reg => reg
                    .Map(m => m.Property((System.Linq.Expressions.Expression<Func<ConventionContext, string>>)null!, "X-Tenant-Id")))));
    }

    [Fact]
    public void Map_Property_Throws_WhenKeyIsWhitespace()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() =>
            services.AddContextR(builder =>
                builder.Add<ConventionContext>(reg => reg
                    .Map(m => m.Property(c => c.TenantId, "   ")))));
    }

    [Fact]
    public void Map_PropertyToProperty_ImplicitlyFinalizesPreviousAsOptional()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .Map(m => m
                    .Property(c => c.TraceId, "X-Trace-Id")
                    .Property(c => c.Payload, "X-Payload")
                    .Required()));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var extracted = propagator.Extract(
            new Dictionary<string, string> { ["X-Payload"] = "required-only" },
            static (c, k) => c.TryGetValue(k, out var v) ? v : null);

        Assert.NotNull(extracted);
        Assert.Null(extracted!.TraceId);
        Assert.Equal("required-only", extracted.Payload);
    }

    [Fact]
    public void Map_BuilderPropertyCalledTwice_FinalizesPendingBeforeSecondCall()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg.Map(m =>
            {
                m.Property(c => c.TraceId, "X-Trace-Id");
                m.Property(c => c.Payload, "X-Payload").Required();
            }));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var extracted = propagator.Extract(
            new Dictionary<string, string> { ["X-Payload"] = "payload-only" },
            static (c, k) => c.TryGetValue(k, out var v) ? v : null);

        Assert.NotNull(extracted);
        Assert.Null(extracted!.TraceId);
        Assert.Equal("payload-only", extracted.Payload);
    }

    [Fact]
    public void Map_ByConvention_ExplicitOptional_OverridesConvention()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<ConventionContext>(reg => reg
                .Map(m => m
                    .ByConvention()
                    .Property(c => c.TenantId, "X-Tenant-Id").Optional()));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<ConventionContext>>();

        var extracted = propagator.Extract(new Dictionary<string, string>(), static (c, k) => c.TryGetValue(k, out var v) ? v : null);
        Assert.Null(extracted);
    }

    [Fact]
    public void Map_DefaultOversizeBehavior_Applies_WhenPropertyHasNoOverride()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<OversizeContext>(reg => reg
                .UseInlineJsonPayloads<OversizeContext>(o =>
                {
                    o.MaxPayloadBytes = 20;
                    o.OversizeBehavior = ContextOversizeBehavior.FailFast;
                })
                .Map(m => m
                    .DefaultOversizeBehavior(ContextOversizeBehavior.SkipProperty)
                    .Property(c => c.Tags, "X-Tags")));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<OversizeContext>>();
        var carrier = new Dictionary<string, string>();

        propagator.Inject(
            new OversizeContext { Tags = Enumerable.Repeat("long-value", 20).ToList() },
            carrier,
            static (c, k, v) => c[k] = v);

        Assert.False(carrier.ContainsKey("X-Tags"));
    }

    [Fact]
    public void Map_WithStrategyPolicy_InvokesPolicyEvaluatorContext()
    {
        CapturingSkipPolicy.LastContext = null;

        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<OversizeContext>(reg => reg
                .UseInlineJsonPayloads(o =>
                {
                    o.MaxPayloadBytes = 20;
                    o.OversizeBehavior = ContextOversizeBehavior.FailFast;
                })
                .UseStrategyPolicy<OversizeContext, CapturingSkipPolicy>()
                .Map(m => m.Property(c => c.Tags, "X-Tags").Optional()));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<OversizeContext>>();
        var carrier = new Dictionary<string, string>();

        propagator.Inject(
            new OversizeContext { Tags = Enumerable.Repeat("long-value", 20).ToList() },
            carrier,
            static (c, k, v) => c[k] = v);

        Assert.False(carrier.ContainsKey("X-Tags"));
        Assert.NotNull(CapturingSkipPolicy.LastContext);
        Assert.Equal(typeof(OversizeContext), CapturingSkipPolicy.LastContext!.ContextType);
        Assert.Equal("X-Tags", CapturingSkipPolicy.LastContext.Key);
        Assert.Equal(PropagationDirection.Inject, CapturingSkipPolicy.LastContext.Direction);
    }

    [Fact]
    public void Map_ByConventionWithDisabledConventions_TreatsMissingNonNullableAsOptional()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<ConventionContext>(reg => reg
                .DisableNullabilityConventions()
                .Map(m => m
                    .ByConvention()
                    .Property(c => c.TenantId, "X-Tenant-Id")));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<ConventionContext>>();

        var extracted = propagator.Extract(
            new Dictionary<string, string>(),
            static (c, k) => c.TryGetValue(k, out var v) ? v : null);

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

    private sealed class ConventionContext
    {
        public required string TenantId { get; set; }
        public string? UserId { get; set; }
    }

    private sealed class OversizeContext
    {
        public List<string>? Tags { get; set; }
    }

    private sealed class CapturingSkipPolicy : IContextPropagationStrategyPolicy<OversizeContext>
    {
        public static ContextPropagationStrategyPolicyContext? LastContext { get; set; }

        public ContextOversizeBehavior Select(ContextPropagationStrategyPolicyContext context)
        {
            LastContext = context;
            return ContextOversizeBehavior.SkipProperty;
        }
    }
}
