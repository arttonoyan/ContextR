using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Propagation.UnitTests;

public sealed class PropagationRegistrationExtensionsCoverageTests
{
    [Fact]
    public void UsePayloadSerializer_AppliesCustomSerialization()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<PayloadContext>(reg => reg
                .UsePayloadSerializer<PayloadContext, PrefixPayloadSerializer>()
                .MapProperty(c => c.Payload, "X-Payload"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<PayloadContext>>();
        var carrier = new Dictionary<string, string>();
        propagator.Inject(
            new PayloadContext { Payload = new PayloadValue { Name = "alice" } },
            carrier,
            static (c, k, v) => c[k] = v);

        Assert.Equal("prefix:alice", carrier["X-Payload"]);

        var extracted = propagator.Extract(
            carrier,
            static (c, k) => c.TryGetValue(k, out var value) ? value : null);

        Assert.NotNull(extracted);
        Assert.Equal("alice", extracted!.Payload?.Name);
    }

    [Fact]
    public void UseTransportPolicy_AppliesSkipPolicy_WhenPayloadIsOversize()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<PayloadContext>(reg => reg
                .UsePayloadSerializer<PayloadContext, PrefixPayloadSerializer>()
                .UseTransportPolicy<PayloadContext, TinySkipTransportPolicy>()
                .MapProperty(c => c.Payload, "X-Payload"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<PayloadContext>>();
        var carrier = new Dictionary<string, string>();

        propagator.Inject(
            new PayloadContext { Payload = new PayloadValue { Name = "this-is-too-long" } },
            carrier,
            static (c, k, v) => c[k] = v);

        Assert.False(carrier.ContainsKey("X-Payload"));
    }

    [Fact]
    public void UsePropagator_RegistersCustomPropagator_AndExecutionScope()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<PayloadContext>(reg => reg
                .UsePropagator<PayloadContext, CustomPropagator>());
        });

        using var provider = services.BuildServiceProvider();
        Assert.IsType<CustomPropagator>(provider.GetRequiredService<IContextPropagator<PayloadContext>>());
        Assert.NotNull(provider.GetRequiredService<IPropagationExecutionScope>());
    }

    [Fact]
    public void OnPropagationFailure_TypedHandler_ResolvesFromDomainRegistry()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .Map(m => m.Property(c => c.Required, "X-Required").Required())
                .OnPropagationFailure<TestContext>(_ => PropagationFailureAction.Throw));

            builder.AddDomain("tenant-a", domain =>
            {
                domain.Add<TestContext>(reg => reg
                    .OnPropagationFailure<TestContext, SkipPropertyFailureHandler>());
            });
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();
        var executionScope = provider.GetRequiredService<IPropagationExecutionScope>();
        using var _ = executionScope.BeginDomainScope("tenant-a");

        var extracted = propagator.Extract(
            new Dictionary<string, string>(),
            static (c, k) => c.TryGetValue(k, out var value) ? value : null);

        Assert.Null(extracted);
    }

    [Fact]
    public void UseStrategyPolicy_TypedPolicy_IsAppliedForOversizeSelection()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<PayloadContext>(reg => reg
                .UsePayloadSerializer<PayloadContext, PrefixPayloadSerializer>()
                .UseTransportPolicy<PayloadContext, TinyFailFastTransportPolicy>()
                .UseStrategyPolicy<PayloadContext, ForceSkipTypedPolicy>()
                .MapProperty(c => c.Payload, "X-Payload"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<PayloadContext>>();
        var carrier = new Dictionary<string, string>();

        propagator.Inject(
            new PayloadContext { Payload = new PayloadValue { Name = "this-is-too-long" } },
            carrier,
            static (c, k, v) => c[k] = v);

        Assert.False(carrier.ContainsKey("X-Payload"));
    }

    [Fact]
    public void UseStrategyPolicy_Delegate_IsApplied_AndRegistryIsReusedAcrossCalls()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<PayloadContext>(reg => reg
                .UsePayloadSerializer<PayloadContext, PrefixPayloadSerializer>()
                .UseTransportPolicy<PayloadContext, TinyFailFastTransportPolicy>()
                .UseStrategyPolicy<PayloadContext>(_ => ContextOversizeBehavior.SkipProperty)
                .UseStrategyPolicy<PayloadContext>(_ => ContextOversizeBehavior.FailFast)
                .MapProperty(c => c.Payload, "X-Payload"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<PayloadContext>>();
        var carrier = new Dictionary<string, string>();

        propagator.Inject(
            new PayloadContext { Payload = new PayloadValue { Name = "this-is-too-long" } },
            carrier,
            static (c, k, v) => c[k] = v);

        // First registration wins; second call still exercises registry reuse path.
        Assert.False(carrier.ContainsKey("X-Payload"));
    }

    private sealed class PayloadContext
    {
        public PayloadValue? Payload { get; set; }
    }

    private sealed class TestContext
    {
        public required string Required { get; set; }
    }

    private sealed class PayloadValue
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class PrefixPayloadSerializer : IContextPayloadSerializer<PayloadContext>
    {
        public bool CanHandle(Type propertyType) => propertyType == typeof(PayloadValue);

        public string Serialize(object value, Type propertyType)
            => $"prefix:{((PayloadValue)value).Name}";

        public bool TryDeserialize(string payload, Type propertyType, out object? value)
        {
            value = new PayloadValue { Name = payload["prefix:".Length..] };
            return true;
        }
    }

    private sealed class TinySkipTransportPolicy : IContextTransportPolicy<PayloadContext>
    {
        public int MaxPayloadBytes => 5;
        public ContextOversizeBehavior OversizeBehavior => ContextOversizeBehavior.SkipProperty;
    }

    private sealed class TinyFailFastTransportPolicy : IContextTransportPolicy<PayloadContext>
    {
        public int MaxPayloadBytes => 5;
        public ContextOversizeBehavior OversizeBehavior => ContextOversizeBehavior.FailFast;
    }

    private sealed class CustomPropagator : IContextPropagator<PayloadContext>
    {
        public void Inject<TCarrier>(PayloadContext context, TCarrier carrier, Action<TCarrier, string, string> setter) { }
        public PayloadContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, string?> getter) => null;
    }

    private sealed class SkipPropertyFailureHandler : IContextPropagationFailureHandler<TestContext>
    {
        public PropagationFailureAction Handle(PropagationFailureContext failure) => PropagationFailureAction.SkipProperty;
    }

    private sealed class ForceSkipTypedPolicy : IContextPropagationStrategyPolicy<PayloadContext>
    {
        public ContextOversizeBehavior Select(ContextPropagationStrategyPolicyContext context)
            => ContextOversizeBehavior.SkipProperty;
    }
}
