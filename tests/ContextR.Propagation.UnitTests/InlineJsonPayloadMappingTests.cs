using Microsoft.Extensions.DependencyInjection;

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

        var ex = Assert.Throws<InvalidOperationException>(() =>
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
