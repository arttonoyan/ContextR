using Microsoft.Extensions.DependencyInjection;
using ContextR.Resolution;

namespace ContextR.IntegrationTests;

public sealed class DependencyInjectionIntegrationTests
{
    [Fact]
    public void AddContextR_RegistersSingletonAccessorAndWriter()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        var rootAccessor = provider.GetRequiredService<IContextAccessor>();
        var scopedAccessor = scope.ServiceProvider.GetRequiredService<IContextAccessor>();
        var rootWriter = provider.GetRequiredService<IContextWriter>();
        var scopedWriter = scope.ServiceProvider.GetRequiredService<IContextWriter>();

        Assert.Same(rootAccessor, scopedAccessor);
        Assert.Same(rootWriter, scopedWriter);
    }

    [Fact]
    public void AddContextR_RegistersResolutionOrchestrator()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<UserContext>(reg => reg
                .UseResolver(_ => new UserContext("resolved-user")));
        });

        using var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<UserContext>>();

        var result = orchestrator.Resolve(new ContextResolutionContext
        {
            Boundary = ContextIngressBoundary.External
        });

        Assert.Equal("resolved-user", result.Context?.UserId);
        Assert.Equal(ContextResolutionSource.Resolver, result.Source);
    }

    [Fact]
    public void UseResolver_TypedOverload_AutoRegistersResolutionServices()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<UserContext>(reg => reg
                .UseResolver<UserContext, TypedUserContextResolver>());
        });

        using var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<UserContext>>();
        var result = orchestrator.Resolve(new ContextResolutionContext { Boundary = ContextIngressBoundary.External });

        Assert.NotNull(orchestrator);
        Assert.Equal("typed-resolver", result.Context?.UserId);
        Assert.Equal(ContextResolutionSource.Resolver, result.Source);
    }

    [Fact]
    public void UseResolver_DelegateOverload_AutoRegistersResolutionServices()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<UserContext>(reg => reg
                .UseResolver(_ => new UserContext("delegate-resolver")));
        });

        using var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<UserContext>>();
        var result = orchestrator.Resolve(new ContextResolutionContext { Boundary = ContextIngressBoundary.External });

        Assert.NotNull(orchestrator);
        Assert.Equal("delegate-resolver", result.Context?.UserId);
        Assert.Equal(ContextResolutionSource.Resolver, result.Source);
    }

    [Fact]
    public void UseResolver_FactoryOverload_AutoRegistersResolutionServices()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<UserContext>(reg => reg
                .UseResolver(_ => new TypedUserContextResolver("factory-resolver")));
        });

        using var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<UserContext>>();
        var result = orchestrator.Resolve(new ContextResolutionContext { Boundary = ContextIngressBoundary.External });

        Assert.NotNull(orchestrator);
        Assert.Equal("factory-resolver", result.Context?.UserId);
        Assert.Equal(ContextResolutionSource.Resolver, result.Source);
    }

    [Fact]
    public void AddContextR_RegistersScopedSnapshot_CapturedAtResolutionTime()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("user-before-scope1"));

        using var scope1 = provider.CreateScope();
        var snapshot1a = scope1.ServiceProvider.GetRequiredService<IContextSnapshot>();

        writer.SetContext(new UserContext("user-after-scope1"));
        var snapshot1b = scope1.ServiceProvider.GetRequiredService<IContextSnapshot>();

        using var scope2 = provider.CreateScope();
        var snapshot2 = scope2.ServiceProvider.GetRequiredService<IContextSnapshot>();

        Assert.Same(snapshot1a, snapshot1b);
        Assert.Equal("user-before-scope1", snapshot1a.GetContext<UserContext>()?.UserId);
        Assert.Equal("user-after-scope1", snapshot2.GetContext<UserContext>()?.UserId);
        Assert.Equal("user-after-scope1", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void AddContextR_Builder_IsChainable_AndInvokesTypedConfiguration()
    {
        var contextBuildersInvoked = 0;

        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder
                .Add<UserContext>(_ => contextBuildersInvoked++)
                .Add<TenantContext>(_ => contextBuildersInvoked++);
        });

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IContextAccessor>();

        Assert.Equal(2, contextBuildersInvoked);
    }

    [Fact]
    public void GetRequiredContext_ThrowsWhenMissing_AndReturnsWhenPresent()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        Assert.Throws<InvalidOperationException>(() => accessor.GetRequiredContext<UserContext>());

        writer.SetContext(new UserContext("user-required"));
        Assert.Equal("user-required", accessor.GetRequiredContext<UserContext>().UserId);
    }

    [Fact]
    public void AddContextR_WithDomainPolicy_AccessorAndWriterAreSameInstance()
    {
        using var provider = CreateDomainProvider();

        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        Assert.IsAssignableFrom<IContextAccessor>(writer);
        Assert.Same(accessor, writer);
    }

    [Fact]
    public void AddContextR_WithDomainPolicy_ScopedSnapshot_CapturesDomainValues()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("default-user"));
        writer.SetContext("web-api", new UserContext("web-user"));

        using var scope = provider.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IContextSnapshot>();

        Assert.Equal("default-user", snapshot.GetContext<UserContext>()?.UserId);
        Assert.Equal("web-user", snapshot.GetContext<UserContext>("web-api")?.UserId);
    }

    [Fact]
    public void AddContextR_WithDomainPolicy_DefaultDomainSelector_PropagatedToScopedSnapshot()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
            builder.AddDomainPolicy(p => p.DefaultDomainSelector = _ => "web-api");
        });

        using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("via-default"));

        using var scope = provider.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IContextSnapshot>();

        Assert.Equal("via-default", snapshot.GetContext<UserContext>()?.UserId);
        Assert.Equal("via-default", snapshot.GetContext<UserContext>("web-api")?.UserId);
    }

    [Fact]
    public void AddContextR_CalledTwice_DoesNotOverrideExistingRegistrations()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder => builder.Add<UserContext>());
        services.AddContextR(builder => builder.Add<UserContext>());

        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("test"));
        Assert.Equal("test", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void GetRequiredContext_Domain_ThrowsWhenMissing_AndReturnsWhenPresent()
    {
        using var provider = CreateDomainProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        Assert.Throws<InvalidOperationException>(() => accessor.GetRequiredContext<UserContext>("web-api"));

        writer.SetContext("web-api", new UserContext("web-required"));
        Assert.Equal("web-required", accessor.GetRequiredContext<UserContext>("web-api").UserId);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<UserContext>();
            builder.Add<TenantContext>();
        });
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateDomainProvider()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<UserContext>();
            builder.Add<TenantContext>();
            builder.AddDomain("web-api", domain =>
            {
                domain.Add<UserContext>();
                domain.Add<TenantContext>();
            });
        });
        return services.BuildServiceProvider();
    }

    private sealed record UserContext(string UserId);
    private sealed record TenantContext(string TenantId);

    private sealed class TypedUserContextResolver : IContextResolver<UserContext>
    {
        private readonly string _userId;

        public TypedUserContextResolver()
            : this("typed-resolver")
        {
        }

        public TypedUserContextResolver(string userId)
        {
            _userId = userId;
        }

        public UserContext? Resolve(ContextResolutionContext context) => new(_userId);
    }
}
