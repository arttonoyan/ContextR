using Microsoft.Extensions.DependencyInjection;

namespace ContextR.UnitTests;

public sealed class PropagatorRegistrationTests
{
    [Fact]
    public void Registration_UsePropagator_RegistersCustomPropagator()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg.UsePropagator<CustomPropagator>());
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetService<IContextPropagator<TestContext>>();

        Assert.NotNull(propagator);
        Assert.IsType<CustomPropagator>(propagator);
    }

    [Fact]
    public void Registration_BuilderExposes_Services()
    {
        var services = new ServiceCollection();
        IServiceCollection? capturedServices = null;

        services.AddContextR(builder =>
        {
            capturedServices = builder.Services;
            builder.Add<TestContext>();
        });

        Assert.Same(services, capturedServices);
    }

    [Fact]
    public void Registration_NullDomain_ForRootRegistration()
    {
        var services = new ServiceCollection();
        string? capturedDomain = null;

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => capturedDomain = reg.Domain);
        });

        Assert.Null(capturedDomain);
    }

    [Fact]
    public void Registration_Domain_IsSet_ForDomainRegistration()
    {
        var services = new ServiceCollection();
        string? capturedDomain = null;

        services.AddContextR(builder =>
        {
            builder
                .Add<TestContext>()
                .AddDomain("my-domain", domain =>
                {
                    domain.Add<TestContext>(reg => capturedDomain = reg.Domain);
                });
        });

        Assert.Equal("my-domain", capturedDomain);
    }

    public class TestContext
    {
        public string? TenantId { get; set; }
        public string? UserId { get; set; }
    }

    private sealed class CustomPropagator : IContextPropagator<TestContext>
    {
        public void Inject<TCarrier>(TestContext context, TCarrier carrier, Action<TCarrier, string, string> setter) { }
        public TestContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, string?> getter) => null;
    }
}
