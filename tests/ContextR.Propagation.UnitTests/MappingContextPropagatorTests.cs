using ContextR.Propagation.Internal;

namespace ContextR.Propagation.UnitTests;

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
    public void Inject_WhenMappingThrows_CanSkipProperty_AndContinue()
    {
        var registry = new ContextPropagationFailureHandlerRegistry<TestContext>();
        registry.TryAdd(null, _ => new DelegateFailureHandler(_ => PropagationFailureAction.SkipProperty));

        var propagator = new MappingContextPropagator<TestContext>(
            [
                new ThrowingMapping(throwOnInject: true, throwOnExtract: false),
                PropertyMapping.Create<TestContext, string?>(c => c.TenantId, "X-Tenant-Id")
            ],
            failureRegistry: registry);

        var carrier = new Dictionary<string, string>();
        propagator.Inject(new TestContext { TenantId = "tenant-1" }, carrier, static (c, k, v) => c[k] = v);

        Assert.Equal("tenant-1", carrier["X-Tenant-Id"]);
    }

    [Fact]
    public void Inject_WhenMappingThrows_AndHandlerSkipsContext_StopsImmediately()
    {
        var registry = new ContextPropagationFailureHandlerRegistry<TestContext>();
        registry.TryAdd(null, _ => new DelegateFailureHandler(_ => PropagationFailureAction.SkipContext));

        var propagator = new MappingContextPropagator<TestContext>(
            [
                new ThrowingMapping(throwOnInject: true, throwOnExtract: false),
                PropertyMapping.Create<TestContext, string?>(c => c.TenantId, "X-Tenant-Id")
            ],
            failureRegistry: registry);

        var carrier = new Dictionary<string, string>();
        propagator.Inject(new TestContext { TenantId = "tenant-1" }, carrier, static (c, k, v) => c[k] = v);

        Assert.Empty(carrier);
    }

    [Fact]
    public void Extract_WhenMappingThrows_CanSkipProperty_AndContinue()
    {
        var registry = new ContextPropagationFailureHandlerRegistry<TestContext>();
        registry.TryAdd(null, _ => new DelegateFailureHandler(_ => PropagationFailureAction.SkipProperty));

        var propagator = new MappingContextPropagator<TestContext>(
            [
                new ThrowingMapping(throwOnInject: false, throwOnExtract: true),
                PropertyMapping.Create<TestContext, string?>(c => c.TenantId, "X-Tenant-Id")
            ],
            failureRegistry: registry);

        var carrier = new Dictionary<string, string> { ["X-Tenant-Id"] = "tenant-2" };
        var extracted = propagator.Extract(carrier, static (c, k) => c.TryGetValue(k, out var v) ? v : null);

        Assert.NotNull(extracted);
        Assert.Equal("tenant-2", extracted!.TenantId);
    }

    [Fact]
    public void Extract_WhenMappingThrows_AndHandlerThrows_BubblesOriginalException()
    {
        var registry = new ContextPropagationFailureHandlerRegistry<TestContext>();
        registry.TryAdd(null, _ => new DelegateFailureHandler(_ => PropagationFailureAction.Throw));

        var propagator = new MappingContextPropagator<TestContext>(
            [new ThrowingMapping(throwOnInject: false, throwOnExtract: true)],
            failureRegistry: registry);

        var ex = Assert.Throws<PropertyMappingException>(() =>
            propagator.Extract(new Dictionary<string, string>(), static (c, k) => c.TryGetValue(k, out var v) ? v : null));

        Assert.Equal(PropagationFailureReason.ParseFailed, ex.Reason);
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

    private sealed class ThrowingMapping(bool throwOnInject, bool throwOnExtract) : IPropertyMapping<TestContext>
    {
        public string Key => "X-Throw";
        public bool IsRequired => false;

        public IEnumerable<KeyValuePair<string, string>> GetValues(TestContext context)
        {
            if (throwOnInject)
            {
                throw new PropertyMappingException(
                    PropagationFailureReason.ParseFailed,
                    "Inject failure.");
            }

            return [];
        }

        public string? GetRawValue<TCarrier>(TCarrier carrier, Func<TCarrier, string, string?> getter) => "raw";

        public bool TrySetValue(TestContext context, string value)
        {
            if (throwOnExtract)
            {
                throw new PropertyMappingException(
                    PropagationFailureReason.ParseFailed,
                    "Extract failure.");
            }

            return false;
        }
    }

    private sealed class DelegateFailureHandler(Func<PropagationFailureContext, PropagationFailureAction> handler)
        : IContextPropagationFailureHandler<TestContext>
    {
        public PropagationFailureAction Handle(PropagationFailureContext failure) => handler(failure);
    }
}
