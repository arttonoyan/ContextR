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

    public class TestContext
    {
        public string? TenantId { get; set; }
        public string? UserId { get; set; }
    }
}
