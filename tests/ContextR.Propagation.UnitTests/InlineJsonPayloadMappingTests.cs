using Microsoft.Extensions.DependencyInjection;
using ContextR.Propagation.Chunking;

namespace ContextR.Propagation.UnitTests;

public sealed class InlineJsonPayloadMappingTests
{
    [Fact]
    public void MapProperty_WithInlineJson_RoundTripsListAndCustomClass()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .UseInlineJsonPayloads<TestContext>()
                .MapProperty(c => c.Tags, "X-Tags")
                .MapProperty(c => c.User, "X-User"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var carrier = new Dictionary<string, string>();
        propagator.Inject(
            new TestContext
            {
                Tags = ["a", "b"],
                User = new UserInfo { Name = "alice", Age = 30 }
            },
            carrier,
            static (c, k, v) => c[k] = v);

        var extracted = propagator.Extract(carrier, static (c, k) => c.TryGetValue(k, out var v) ? v : null);

        Assert.NotNull(extracted);
        Assert.Equal(["a", "b"], extracted.Tags);
        Assert.NotNull(extracted.User);
        Assert.Equal("alice", extracted.User!.Name);
        Assert.Equal(30, extracted.User.Age);
    }

    [Fact]
    public void MapProperty_WithInlineJsonAndFailFastPolicy_ThrowsWhenOversize()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .UseInlineJsonPayloads<TestContext>(o =>
                {
                    o.MaxPayloadBytes = 20;
                    o.OversizeBehavior = ContextOversizeBehavior.FailFast;
                })
                .MapProperty(c => c.Tags, "X-Tags"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var carrier = new Dictionary<string, string>();
        var context = new TestContext { Tags = Enumerable.Repeat("value", 10).ToList() };

        var ex = Assert.ThrowsAny<InvalidOperationException>(() =>
            propagator.Inject(context, carrier, static (c, k, v) => c[k] = v));

        Assert.Contains("exceeded limit", ex.Message);
    }

    [Fact]
    public void MapProperty_WithInlineJsonAndSkipPolicy_SkipsOversizeProperty()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .UseInlineJsonPayloads<TestContext>(o =>
                {
                    o.MaxPayloadBytes = 20;
                    o.OversizeBehavior = ContextOversizeBehavior.SkipProperty;
                })
                .MapProperty(c => c.Tags, "X-Tags")
                .MapProperty(c => c.TenantId, "X-Tenant-Id"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var carrier = new Dictionary<string, string>();
        var context = new TestContext
        {
            TenantId = "t1",
            Tags = Enumerable.Repeat("value", 10).ToList()
        };

        propagator.Inject(context, carrier, static (c, k, v) => c[k] = v);

        Assert.Equal("t1", carrier["X-Tenant-Id"]);
        Assert.False(carrier.ContainsKey("X-Tags"));
    }

    [Fact]
    public void MapProperty_WithInlineJsonAndChunkPolicy_SplitsAndRoundTripsOversizePayload()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .UseInlineJsonPayloads<TestContext>(o =>
                {
                    o.MaxPayloadBytes = 20;
                    o.OversizeBehavior = ContextOversizeBehavior.ChunkProperty;
                })
                .UseChunkingPayloads<TestContext>()
                .MapProperty(c => c.Tags, "X-Tags"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var carrier = new Dictionary<string, string>();
        propagator.Inject(
            new TestContext { Tags = Enumerable.Repeat("value", 10).ToList() },
            carrier,
            static (c, k, v) => c[k] = v);

        Assert.False(carrier.ContainsKey("X-Tags"));
        Assert.Contains("X-Tags__chunks", carrier.Keys);
        Assert.Contains(carrier.Keys, k => k.StartsWith("X-Tags__chunk_", StringComparison.Ordinal));

        var extracted = propagator.Extract(carrier, static (c, k) => c.TryGetValue(k, out var v) ? v : null);
        Assert.NotNull(extracted);
        Assert.NotNull(extracted!.Tags);
        Assert.Equal(10, extracted.Tags!.Count);
        Assert.All(extracted.Tags, tag => Assert.Equal("value", tag));
    }

    [Fact]
    public void MapProperty_WithChunkPolicy_MissingAnyChunk_TreatsPropertyAsMissing()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .UseInlineJsonPayloads<TestContext>(o =>
                {
                    o.MaxPayloadBytes = 20;
                    o.OversizeBehavior = ContextOversizeBehavior.ChunkProperty;
                })
                .UseChunkingPayloads<TestContext>()
                .MapProperty(c => c.Tags, "X-Tags")
                .MapProperty(c => c.TenantId, "X-Tenant-Id"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var carrier = new Dictionary<string, string>();
        propagator.Inject(
            new TestContext
            {
                TenantId = "t1",
                Tags = Enumerable.Repeat("value", 10).ToList()
            },
            carrier,
            static (c, k, v) => c[k] = v);

        var firstChunkKey = carrier.Keys.First(k => k.StartsWith("X-Tags__chunk_", StringComparison.Ordinal));
        carrier.Remove(firstChunkKey);

        var extracted = propagator.Extract(carrier, static (c, k) => c.TryGetValue(k, out var v) ? v : null);
        Assert.NotNull(extracted);
        Assert.Equal("t1", extracted!.TenantId);
        Assert.Null(extracted.Tags);
    }

    [Fact]
    public void MapDsl_HybridPolicy_UsesPropertyOverrideOverContextDefault()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .UseInlineJsonPayloads<TestContext>(o =>
                {
                    o.MaxPayloadBytes = 20;
                    o.OversizeBehavior = ContextOversizeBehavior.SkipProperty;
                })
                .UseChunkingPayloads<TestContext>()
                .Map(m => m
                    .DefaultOversizeBehavior(ContextOversizeBehavior.SkipProperty)
                    .Property(c => c.Tags, "X-Tags").OversizeBehavior(ContextOversizeBehavior.ChunkProperty).Optional()
                    .Property(c => c.User, "X-User").Optional()));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var carrier = new Dictionary<string, string>();
        propagator.Inject(
            new TestContext
            {
                Tags = Enumerable.Repeat("value", 10).ToList(),
                User = new UserInfo { Name = new string('x', 64), Age = 33 }
            },
            carrier,
            static (c, k, v) => c[k] = v);

        // Tags use property-level ChunkProperty override.
        Assert.Contains("X-Tags__chunks", carrier.Keys);
        Assert.Contains(carrier.Keys, k => k.StartsWith("X-Tags__chunk_", StringComparison.Ordinal));

        // User stays on context default SkipProperty, so it is dropped.
        Assert.DoesNotContain("X-User", carrier.Keys);
        Assert.DoesNotContain(carrier.Keys, k => k.StartsWith("X-User__chunk_", StringComparison.Ordinal));
    }

    public sealed class TestContext
    {
        public string? TenantId { get; set; }
        public List<string>? Tags { get; set; }
        public UserInfo? User { get; set; }
    }

    public sealed class UserInfo
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }
}
