using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Propagation.UnitTests;

public sealed class MapPropertyRegistrationTests
{
    [Fact]
    public void MapProperty_RegistersPropagator_InDI()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .MapProperty(c => c.TenantId, "X-Tenant-Id")
                .MapProperty(c => c.UserId, "X-User-Id"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetService<IContextPropagator<TestContext>>();

        Assert.NotNull(propagator);
    }

    [Fact]
    public void MapProperty_RegistersPropagationExecutionScope_InDI()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .MapProperty(c => c.TenantId, "X-Tenant-Id"));
        });

        using var provider = services.BuildServiceProvider();
        var executionScope = provider.GetService<IPropagationExecutionScope>();

        Assert.NotNull(executionScope);
    }

    [Fact]
    public void MapProperty_PropagatorRoundTrips_ThroughDI()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .MapProperty(c => c.TenantId, "X-Tenant-Id")
                .MapProperty(c => c.UserId, "X-User-Id"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        var carrier = new Dictionary<string, string>();
        propagator.Inject(new TestContext { TenantId = "t1", UserId = "u42" }, carrier,
            static (c, k, v) => c[k] = v);

        Assert.Equal("t1", carrier["X-Tenant-Id"]);
        Assert.Equal("u42", carrier["X-User-Id"]);

        var extracted = propagator.Extract(carrier,
            static (c, k) => c.TryGetValue(k, out var v) ? v : null);

        Assert.NotNull(extracted);
        Assert.Equal("t1", extracted.TenantId);
        Assert.Equal("u42", extracted.UserId);
    }

    [Fact]
    public void UsePropagator_TakesPrecedence_OverMapProperty_WhenRegisteredFirst()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .UsePropagator<TestContext, CustomPropagator>());
        });

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .MapProperty(c => c.TenantId, "X-Tenant-Id"));
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetRequiredService<IContextPropagator<TestContext>>();

        Assert.IsType<CustomPropagator>(propagator);
    }

    [Fact]
    public void MapProperty_CanBeChained_WithTransportExtensions()
    {
        var services = new ServiceCollection();
        IContextTypeBuilder<TestContext>? capturedBuilder = null;

        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg =>
            {
                capturedBuilder = reg
                    .MapProperty(c => c.TenantId, "X-Tenant-Id")
                    .MapProperty(c => c.UserId, "X-User-Id");
            });
        });

        Assert.NotNull(capturedBuilder);
    }

    [Fact]
    public void MapProperty_RegistersForDomainScoped_Context()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder
                .Add<TestContext>()
                .AddDomain("my-domain", domain =>
                {
                    domain.Add<TestContext>(reg => reg
                        .MapProperty(c => c.TenantId, "X-Tenant-Id"));
                });
        });

        using var provider = services.BuildServiceProvider();
        var propagator = provider.GetService<IContextPropagator<TestContext>>();

        Assert.NotNull(propagator);
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
