using ContextR.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.UnitTests;

public sealed class MappingContextPropagatorTests
{
    [Fact]
    public void Inject_WritesAllMappedProperties_ToCarrier()
    {
        var propagator = BuildPropagator();
        var context = new TestContext { TenantId = "t1", UserId = "u42" };
        var carrier = new Dictionary<string, string>();

        propagator.Inject(context, carrier, static (c, k, v) => c[k] = v);

        Assert.Equal("t1", carrier["X-Tenant-Id"]);
        Assert.Equal("u42", carrier["X-User-Id"]);
    }

    [Fact]
    public void Inject_SkipsNullProperties()
    {
        var propagator = BuildPropagator();
        var context = new TestContext { TenantId = "t1" };
        var carrier = new Dictionary<string, string>();

        propagator.Inject(context, carrier, static (c, k, v) => c[k] = v);

        Assert.Equal("t1", carrier["X-Tenant-Id"]);
        Assert.False(carrier.ContainsKey("X-User-Id"));
    }

    [Fact]
    public void Extract_ReturnsContext_WhenAllKeysPresent()
    {
        var propagator = BuildPropagator();
        var carrier = new Dictionary<string, string>
        {
            ["X-Tenant-Id"] = "t1",
            ["X-User-Id"] = "u42"
        };

        var context = propagator.Extract(carrier, static (c, k) => c.TryGetValue(k, out var v) ? v : null);

        Assert.NotNull(context);
        Assert.Equal("t1", context.TenantId);
        Assert.Equal("u42", context.UserId);
    }

    [Fact]
    public void Extract_ReturnsPartialContext_WhenSomeKeysPresent()
    {
        var propagator = BuildPropagator();
        var carrier = new Dictionary<string, string>
        {
            ["X-Tenant-Id"] = "t1"
        };

        var context = propagator.Extract(carrier, static (c, k) => c.TryGetValue(k, out var v) ? v : null);

        Assert.NotNull(context);
        Assert.Equal("t1", context.TenantId);
        Assert.Null(context.UserId);
    }

    [Fact]
    public void Extract_ReturnsNull_WhenNoKeysPresent()
    {
        var propagator = BuildPropagator();
        var carrier = new Dictionary<string, string>();

        var context = propagator.Extract(carrier, static (c, k) => c.TryGetValue(k, out var v) ? v : null);

        Assert.Null(context);
    }

    [Fact]
    public void MapProperty_WorksWithIntProperties()
    {
        var mappings = new IPropertyMapping<IntContext>[]
        {
            PropertyMapping.Create<IntContext, int>(c => c.Count, "X-Count")
        };
        var propagator = new MappingContextPropagator<IntContext>(mappings);

        var carrier = new Dictionary<string, string> { ["X-Count"] = "42" };
        var context = propagator.Extract(carrier, static (c, k) => c.TryGetValue(k, out var v) ? v : null);

        Assert.NotNull(context);
        Assert.Equal(42, context.Count);

        var outCarrier = new Dictionary<string, string>();
        propagator.Inject(new IntContext { Count = 7 }, outCarrier, static (c, k, v) => c[k] = v);
        Assert.Equal("7", outCarrier["X-Count"]);
    }

    [Fact]
    public void MapProperty_WorksWithGuidProperties()
    {
        var expected = Guid.NewGuid();
        var mappings = new IPropertyMapping<GuidContext>[]
        {
            PropertyMapping.Create<GuidContext, Guid>(c => c.CorrelationId, "X-Correlation-Id")
        };
        var propagator = new MappingContextPropagator<GuidContext>(mappings);

        var carrier = new Dictionary<string, string> { ["X-Correlation-Id"] = expected.ToString() };
        var context = propagator.Extract(carrier, static (c, k) => c.TryGetValue(k, out var v) ? v : null);

        Assert.NotNull(context);
        Assert.Equal(expected, context.CorrelationId);
    }

    [Fact]
    public void Constructor_Throws_WhenContextTypeLacksParameterlessConstructor()
    {
        var mappings = new IPropertyMapping<NoDefaultCtorContext>[]
        {
            PropertyMapping.Create<NoDefaultCtorContext, string>(c => c.Value, "X-Value")
        };

        Assert.Throws<InvalidOperationException>(() => new MappingContextPropagator<NoDefaultCtorContext>(mappings));
    }

    [Fact]
    public void Registration_MapProperty_RegistersPropagator()
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
    public void Registration_Throws_WhenBothUsePropagatorAndMapPropertyUsed()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<TestContext>(reg => reg
                    .UsePropagator<CustomPropagator>()
                    .MapProperty(c => c.TenantId, "X-Tenant-Id"));
            });
        });
    }

    [Fact]
    public void Registration_Throws_WhenBothMapPropertyAndUsePropagatorUsed()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<TestContext>(reg => reg
                    .MapProperty(c => c.TenantId, "X-Tenant-Id")
                    .UsePropagator<CustomPropagator>());
            });
        });
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

    private static MappingContextPropagator<TestContext> BuildPropagator()
    {
        var mappings = new IPropertyMapping<TestContext>[]
        {
            PropertyMapping.Create<TestContext, string?>(c => c.TenantId, "X-Tenant-Id"),
            PropertyMapping.Create<TestContext, string?>(c => c.UserId, "X-User-Id"),
        };
        return new MappingContextPropagator<TestContext>(mappings);
    }

    public class TestContext
    {
        public string? TenantId { get; set; }
        public string? UserId { get; set; }
    }

    public class IntContext
    {
        public int Count { get; set; }
    }

    public class GuidContext
    {
        public Guid CorrelationId { get; set; }
    }

    public class NoDefaultCtorContext
    {
        public NoDefaultCtorContext(string value) => Value = value;
        public string Value { get; set; }
    }

    private sealed class CustomPropagator : IContextPropagator<TestContext>
    {
        public void Inject<TCarrier>(TestContext context, TCarrier carrier, Action<TCarrier, string, string> setter) { }
        public TestContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, string?> getter) => null;
    }
}
