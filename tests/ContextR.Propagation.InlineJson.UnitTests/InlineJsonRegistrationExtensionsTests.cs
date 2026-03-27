using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Propagation.InlineJson.UnitTests;

public sealed class InlineJsonRegistrationExtensionsTests
{
    [Fact]
    public void UseInlineJsonPayloads_RegistersSerializerAndDefaultPolicy()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg.UseInlineJsonPayloads<TestContext>());
        });

        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetService<IContextPayloadSerializer<TestContext>>();
        var policy = provider.GetService<IContextTransportPolicy<TestContext>>();

        Assert.NotNull(serializer);
        Assert.NotNull(policy);
        Assert.Equal(4096, policy.MaxPayloadBytes);
        Assert.Equal(ContextOversizeBehavior.FailFast, policy.OversizeBehavior);
    }

    [Fact]
    public void UseInlineJsonPayloads_AppliesConfiguredOptions()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg.UseInlineJsonPayloads<TestContext>(o =>
            {
                o.MaxPayloadBytes = 128;
                o.OversizeBehavior = ContextOversizeBehavior.SkipProperty;
            }));
        });

        using var provider = services.BuildServiceProvider();
        var policy = provider.GetRequiredService<IContextTransportPolicy<TestContext>>();

        Assert.Equal(128, policy.MaxPayloadBytes);
        Assert.Equal(ContextOversizeBehavior.SkipProperty, policy.OversizeBehavior);
    }

    [Fact]
    public void UseInlineJsonPayloads_ThrowsWhenBuilderIsNull()
    {
        IContextTypeBuilder<TestContext>? builder = null;

        Assert.Throws<ArgumentNullException>(() =>
            ContextRInlineJsonRegistrationExtensions.UseInlineJsonPayloads<TestContext>(builder!));
    }

    [Fact]
    public void UseInlineJsonPayloads_ThrowsWhenConfigureIsNull()
    {
        IContextTypeBuilder<TestContext>? captured = null;
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => captured = reg);
        });

        Assert.NotNull(captured);

        Assert.Throws<ArgumentNullException>(() =>
            ContextRInlineJsonRegistrationExtensions.UseInlineJsonPayloads<TestContext>(captured!, null!));
    }

    private sealed class TestContext;
}
