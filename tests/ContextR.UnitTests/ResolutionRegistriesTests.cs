using ContextR.Resolution;
using ContextR.Resolution.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.UnitTests;

public sealed class ResolutionRegistriesTests
{
    [Fact]
    public void PolicyRegistry_Throws_ForNullFactory_AndNullServices()
    {
        var registry = new ContextResolutionPolicyRegistry<TestContext>();

        Assert.Throws<ArgumentNullException>(() => registry.TryAdd(null, null!));
        Assert.Throws<ArgumentNullException>(() => registry.Resolve(null!, null));
    }

    [Fact]
    public void PolicyRegistry_UsesDomainFallback_AndIgnoresDuplicateRegistration()
    {
        var registry = new ContextResolutionPolicyRegistry<TestContext>();
        registry.TryAdd(null, _ => new StaticPolicy("default"));
        registry.TryAdd("orders", _ => new StaticPolicy("orders-first"));
        registry.TryAdd("orders", _ => new StaticPolicy("orders-second"));
        registry.TryAdd(string.Empty, _ => new StaticPolicy("empty-domain-ignored"));

        using var provider = new ServiceCollection().BuildServiceProvider();

        var orders = registry.Resolve(provider, "orders");
        var missing = registry.Resolve(provider, "missing");
        var empty = registry.Resolve(provider, string.Empty);

        Assert.Equal("orders-first", ((StaticPolicy)orders!).Name);
        Assert.Equal("default", ((StaticPolicy)missing!).Name);
        Assert.Equal("default", ((StaticPolicy)empty!).Name);
    }

    [Fact]
    public void ResolverRegistry_Throws_ForNullFactory_AndNullServices()
    {
        var registry = new ContextResolverRegistry<TestContext>();

        Assert.Throws<ArgumentNullException>(() => registry.TryAdd(null, null!));
        Assert.Throws<ArgumentNullException>(() => registry.Resolve(null!, null));
    }

    [Fact]
    public void ResolverRegistry_UsesDomainFallback_AndIgnoresDuplicateRegistration()
    {
        var registry = new ContextResolverRegistry<TestContext>();
        registry.TryAdd(null, _ => new StaticResolver("default"));
        registry.TryAdd("orders", _ => new StaticResolver("orders-first"));
        registry.TryAdd("orders", _ => new StaticResolver("orders-second"));
        registry.TryAdd(string.Empty, _ => new StaticResolver("empty-domain-ignored"));

        using var provider = new ServiceCollection().BuildServiceProvider();

        var orders = registry.Resolve(provider, "orders");
        var missing = registry.Resolve(provider, "missing");
        var empty = registry.Resolve(provider, string.Empty);

        Assert.Equal("orders-first", ((StaticResolver)orders!).Name);
        Assert.Equal("default", ((StaticResolver)missing!).Name);
        Assert.Equal("default", ((StaticResolver)empty!).Name);
    }

    [Fact]
    public void Registries_Resolve_ReturnsNull_WhenNoRegistrationsExist()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var policyRegistry = new ContextResolutionPolicyRegistry<TestContext>();
        var resolverRegistry = new ContextResolverRegistry<TestContext>();

        Assert.Null(policyRegistry.Resolve(provider, "missing"));
        Assert.Null(resolverRegistry.Resolve(provider, "missing"));
    }

    private sealed record TestContext(string Value);

    private sealed class StaticPolicy(string name) : IContextResolutionPolicy<TestContext>
    {
        public string Name { get; } = name;

        public ContextResolutionResult<TestContext> Resolve(ContextResolutionPolicyContext<TestContext> context)
            => new()
            {
                Context = new TestContext(Name),
                Source = ContextResolutionSource.Policy
            };
    }

    private sealed class StaticResolver(string name) : IContextResolver<TestContext>
    {
        public string Name { get; } = name;
        public TestContext? Resolve(ContextResolutionContext context) => new(Name);
    }
}
