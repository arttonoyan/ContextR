using ContextR.Hosting.AspNetCore.Internal;
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

    public class TestContext
    {
        public string? TenantId { get; set; }
        public string? UserId { get; set; }
    }
}
