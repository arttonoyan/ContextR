using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Propagation.UnitTests;

public sealed class StrategyPolicyRegistryAndRegistrationTests
{
    [Fact]
    public void UseStrategyPolicy_Delegate_Throws_WhenPolicyIsNull()
    {
        var services = new ServiceCollection();
        IContextRegistrationBuilder<TestContext>? capturedBuilder = null;
        services.AddContextR(builder => builder.Add<TestContext>(reg => capturedBuilder = reg));

        Assert.NotNull(capturedBuilder);
        Assert.Throws<ArgumentNullException>(() =>
            capturedBuilder!.UseStrategyPolicy<TestContext>(
                (Func<ContextPropagationStrategyPolicyContext, ContextOversizeBehavior>)null!));
    }

    [Fact]
    public void UseStrategyPolicy_Factory_Throws_WhenFactoryIsNull()
    {
        var services = new ServiceCollection();
        IContextRegistrationBuilder<TestContext>? capturedBuilder = null;
        services.AddContextR(builder => builder.Add<TestContext>(reg => capturedBuilder = reg));

        Assert.NotNull(capturedBuilder);
        Assert.Throws<ArgumentNullException>(() =>
            capturedBuilder!.UseStrategyPolicy<TestContext>(
                (Func<IServiceProvider, Func<ContextPropagationStrategyPolicyContext, ContextOversizeBehavior>>)null!));
    }

    [Fact]
    public void OnPropagationFailure_Throws_WhenHandlerIsNull()
    {
        var services = new ServiceCollection();
        IContextRegistrationBuilder<TestContext>? capturedBuilder = null;
        services.AddContextR(builder => builder.Add<TestContext>(reg => capturedBuilder = reg));

        Assert.NotNull(capturedBuilder);
        Assert.Throws<ArgumentNullException>(() =>
            capturedBuilder!.OnPropagationFailure<TestContext>(null!));
    }

    [Fact]
    public void StrategyPolicyRegistry_Resolve_FallsBackToDefault_WhenDomainIsMissing()
    {
        var registry = new ContextPropagationStrategyPolicyRegistry<TestContext>();
        registry.TryAdd(null, _ => new ConstantPolicy(ContextOversizeBehavior.SkipProperty));

        var resolved = registry.Resolve(new ServiceCollection().BuildServiceProvider(), "orders");

        Assert.NotNull(resolved);
        Assert.Equal(
            ContextOversizeBehavior.SkipProperty,
            resolved!.Select(new ContextPropagationStrategyPolicyContext
            {
                ContextType = typeof(TestContext),
                Key = "X-Trace-Id",
                PropertyType = typeof(string),
                Direction = PropagationDirection.Inject
            }));
    }

    [Fact]
    public void StrategyPolicyRegistry_TryAdd_DoesNotOverrideExistingDomainFactory()
    {
        var registry = new ContextPropagationStrategyPolicyRegistry<TestContext>();
        registry.TryAdd("orders", _ => new ConstantPolicy(ContextOversizeBehavior.SkipProperty));
        registry.TryAdd("orders", _ => new ConstantPolicy(ContextOversizeBehavior.ChunkProperty));

        var resolved = registry.Resolve(new ServiceCollection().BuildServiceProvider(), "orders");

        Assert.NotNull(resolved);
        Assert.Equal(
            ContextOversizeBehavior.SkipProperty,
            resolved!.Select(new ContextPropagationStrategyPolicyContext
            {
                ContextType = typeof(TestContext),
                Key = "X-Trace-Id",
                PropertyType = typeof(string),
                Direction = PropagationDirection.Inject
            }));
    }

    [Fact]
    public void StrategyPolicyRegistry_EmptyDomain_UsesDefaultDomainBucket()
    {
        var registry = new ContextPropagationStrategyPolicyRegistry<TestContext>();
        registry.TryAdd(string.Empty, _ => new ConstantPolicy(ContextOversizeBehavior.ChunkProperty));

        var resolved = registry.Resolve(new ServiceCollection().BuildServiceProvider(), null);

        Assert.NotNull(resolved);
        Assert.Equal(
            ContextOversizeBehavior.ChunkProperty,
            resolved!.Select(new ContextPropagationStrategyPolicyContext
            {
                ContextType = typeof(TestContext),
                Key = "X-Trace-Id",
                PropertyType = typeof(string),
                Direction = PropagationDirection.Inject
            }));
    }

    private sealed class ConstantPolicy(ContextOversizeBehavior behavior)
        : IContextPropagationStrategyPolicy<TestContext>
    {
        public ContextOversizeBehavior Select(ContextPropagationStrategyPolicyContext context) => behavior;
    }

    private sealed class TestContext
    {
        public string? TenantId { get; set; }
    }
}
