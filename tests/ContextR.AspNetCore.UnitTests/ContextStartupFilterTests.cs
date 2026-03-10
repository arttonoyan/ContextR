using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.AspNetCore.UnitTests;

public sealed class ContextStartupFilterTests
{
    [Fact]
    public void UseAspNetCore_RegistersStartupFilter()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")
            .UseAspNetCore()));

        var filterDescriptors = services
            .Where(d => d.ServiceType == typeof(IStartupFilter))
            .ToList();

        Assert.Single(filterDescriptors);
    }

    [Fact]
    public void UseAspNetCore_WithConfigure_RegistersStartupFilter_AndOptions()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")
            .UseAspNetCore(o => o.Enforcement(e => e.Mode = ContextIngressEnforcementMode.FailRequest))));

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ContextRAspNetCoreOptionsRegistry<TestContext>>();
        var options = registry.Resolve(provider, domain: null);

        Assert.Equal(ContextIngressEnforcementMode.FailRequest, options.EnforcementOptions.Mode);
    }

    [Fact]
    public void UseAspNetCore_WithFactoryConfigure_CanResolveServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<MarkerService>();

        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")
            .UseAspNetCore(sp =>
            {
                _ = sp.GetRequiredService<MarkerService>();
                return new ContextRAspNetCoreOptions<TestContext>()
                    .Enforcement(e => e.Mode = ContextIngressEnforcementMode.ObserveOnly);
            })));

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ContextRAspNetCoreOptionsRegistry<TestContext>>();
        var options = registry.Resolve(provider, domain: null);

        Assert.Equal(ContextIngressEnforcementMode.ObserveOnly, options.EnforcementOptions.Mode);
    }

    [Fact]
    public void UseAspNetCore_WithServiceProviderAndOptionsCallback_CanResolveServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<MarkerService>();

        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")
            .UseAspNetCore((sp, o) =>
            {
                _ = sp.GetRequiredService<MarkerService>();
                o.Enforcement(e => e.Mode = ContextIngressEnforcementMode.FailRequest);
            })));

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ContextRAspNetCoreOptionsRegistry<TestContext>>();
        var options = registry.Resolve(provider, domain: null);

        Assert.Equal(ContextIngressEnforcementMode.FailRequest, options.EnforcementOptions.Mode);
    }

    [Fact]
    public void UseAspNetCore_CanBeChainedWithOtherMethods()
    {
        var services = new ServiceCollection();

        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")
            .UseAspNetCore()
            .MapProperty(c => c.UserId, "X-User-Id")));

        var propagator = services.BuildServiceProvider()
            .GetService<IContextPropagator<TestContext>>();

        Assert.NotNull(propagator);
    }

    [Fact]
    public void UseAspNetCore_CapturesDomain_WhenRegisteredInsideAddDomain()
    {
        var services = new ServiceCollection();

        services.AddContextR(ctx =>
        {
            ctx.Add<TestContext>()
               .AddDomain("my-domain", d => d.Add<TestContext>(reg => reg
                   .MapProperty(c => c.TenantId, "X-Tenant-Id")
                   .UseAspNetCore()));
        });

        var filterDescriptor = services.Single(d => d.ServiceType == typeof(IStartupFilter));

        Assert.NotNull(filterDescriptor.ImplementationFactory);
        var filter = filterDescriptor.ImplementationFactory!(null!) as ContextStartupFilter<TestContext>;
        Assert.NotNull(filter);
    }

    [Fact]
    public void UseAspNetCore_NullDomain_WhenRegisteredAtRoot()
    {
        var services = new ServiceCollection();

        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")
            .UseAspNetCore()));

        var filterDescriptor = services.Single(d => d.ServiceType == typeof(IStartupFilter));
        Assert.NotNull(filterDescriptor.ImplementationFactory);
    }

    [Fact]
    public void StartupFilter_Configure_CallsNext()
    {
        var filter = new ContextStartupFilter<TestContext>(domain: null);
        var nextCalled = false;

        var configuredAction = filter.Configure(app => nextCalled = true);

        var appBuilder = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        configuredAction(appBuilder);

        Assert.True(nextCalled);
    }

    [Fact]
    public void StartupFilter_Configure_WithDomain_CallsNext()
    {
        var filter = new ContextStartupFilter<TestContext>(domain: "my-domain");
        var nextCalled = false;

        var configuredAction = filter.Configure(app => nextCalled = true);

        var appBuilder = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        configuredAction(appBuilder);

        Assert.True(nextCalled);
    }

    [Fact]
    public void OptionsRegistry_Resolve_ReturnsDomainSpecific_ThenDefault_ThenNewOptions()
    {
        var registry = new ContextRAspNetCoreOptionsRegistry<TestContext>();
        registry.TryAdd(null, _ => new ContextRAspNetCoreOptions<TestContext>()
            .Enforcement(e => e.Mode = ContextIngressEnforcementMode.ObserveOnly));
        registry.TryAdd("orders", _ => new ContextRAspNetCoreOptions<TestContext>()
            .Enforcement(e => e.Mode = ContextIngressEnforcementMode.FailRequest));

        using var provider = new ServiceCollection().BuildServiceProvider();

        var domain = registry.Resolve(provider, "orders");
        var fallback = registry.Resolve(provider, "missing");

        var emptyRegistry = new ContextRAspNetCoreOptionsRegistry<TestContext>();
        var none = emptyRegistry.Resolve(provider, "missing");

        Assert.Equal(ContextIngressEnforcementMode.FailRequest, domain.EnforcementOptions.Mode);
        Assert.Equal(ContextIngressEnforcementMode.ObserveOnly, fallback.EnforcementOptions.Mode);
        Assert.Equal(ContextIngressEnforcementMode.Disabled, none.EnforcementOptions.Mode);
    }

    [Fact]
    public void OptionsRegistry_DefaultFactory_IsNotOverwritten()
    {
        var registry = new ContextRAspNetCoreOptionsRegistry<TestContext>();
        registry.TryAdd(null, _ => new ContextRAspNetCoreOptions<TestContext>()
            .Enforcement(e => e.Mode = ContextIngressEnforcementMode.ObserveOnly));
        registry.TryAdd(null, _ => new ContextRAspNetCoreOptions<TestContext>()
            .Enforcement(e => e.Mode = ContextIngressEnforcementMode.FailRequest));

        using var provider = new ServiceCollection().BuildServiceProvider();
        var resolved = registry.Resolve(provider, null);

        Assert.Equal(ContextIngressEnforcementMode.ObserveOnly, resolved.EnforcementOptions.Mode);
    }

    [Fact]
    public void UseAspNetCore_MultipleRegistrations_ReuseExistingOptionsRegistry()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.TenantId, "X-Tenant-Id")
            .UseAspNetCore()));
        services.AddContextR(ctx => ctx.Add<TestContext>(reg => reg
            .MapProperty(c => c.UserId, "X-User-Id")
            .UseAspNetCore(o => o.Enforcement(e => e.Mode = ContextIngressEnforcementMode.FailRequest))));

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ContextRAspNetCoreOptionsRegistry<TestContext>>();
        var options = registry.Resolve(provider, null);

        // Default registration keeps first factory due TryAdd semantics.
        Assert.Equal(ContextIngressEnforcementMode.Disabled, options.EnforcementOptions.Mode);
    }

    public class TestContext
    {
        public string? TenantId { get; set; }
        public string? UserId { get; set; }
    }

    private sealed class MarkerService;
}
