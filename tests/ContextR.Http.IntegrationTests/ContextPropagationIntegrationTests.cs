using System.Text.Json;
using ContextR.Http.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Http.IntegrationTests;

public sealed class ContextPropagationIntegrationTests
{
    // ──────────────────────────────────────────────────────────────
    //  Middleware Extraction
    // ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("trace-abc", "span-def", "trace-abc", "span-def")]
    [InlineData("trace-only", null, "trace-only", null)]
    [InlineData(null, "span-only", null, "span-only")]
    [InlineData(null, null, null, null)]
    public async Task Middleware_ExtractsContext_BasedOnIncomingHeaders(
        string? traceId, string? spanId, string? expectedTrace, string? expectedSpan)
    {
        await using var app = await CreateWithGlobalPropagationAsync();

        var headers = BuildHeaders(("X-Trace-Id", traceId), ("X-Span-Id", spanId));
        var json = await app.GetJsonAsync("/context", headers);

        AssertJsonStringOrNull(json, "traceId", expectedTrace);
        AssertJsonStringOrNull(json, "spanId", expectedSpan);
    }

    // ──────────────────────────────────────────────────────────────
    //  Global HTTP Propagation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GlobalPropagation_InjectsAllContextHeaders_IntoOutgoingRequest()
    {
        await using var app = await CreateWithGlobalPropagationAsync();

        var json = await app.GetJsonAsync("/propagate",
            ("X-Trace-Id", "trace-out"), ("X-Span-Id", "span-out"));

        var extracted = json.GetProperty("extracted");
        Assert.Equal("trace-out", extracted.GetProperty("traceId").GetString());
        Assert.Equal("span-out", extracted.GetProperty("spanId").GetString());

        var propagated = json.GetProperty("propagatedHeaders");
        Assert.Equal("trace-out", propagated.GetProperty("X-Trace-Id").GetString());
        Assert.Equal("span-out", propagated.GetProperty("X-Span-Id").GetString());
    }

    [Fact]
    public async Task GlobalPropagation_DoesNotInjectHeaders_WhenNoContextPresent()
    {
        await using var app = await CreateWithGlobalPropagationAsync();

        var json = await app.GetJsonAsync("/propagate");

        var propagated = json.GetProperty("propagatedHeaders");
        Assert.False(propagated.TryGetProperty("X-Trace-Id", out _));
        Assert.False(propagated.TryGetProperty("X-Span-Id", out _));
    }

    [Fact]
    public async Task GlobalPropagation_InjectsHeaders_IntoAllNamedClients()
    {
        await using var app = await CreateWithGlobalPropagationAsync();

        var json = await app.GetJsonAsync("/propagate/multi",
            ("X-Trace-Id", "trace-multi"), ("X-Span-Id", "span-multi"));

        var h1 = json.GetProperty("client1Headers");
        var h2 = json.GetProperty("client2Headers");

        Assert.Equal("trace-multi", h1.GetProperty("X-Trace-Id").GetString());
        Assert.Equal("span-multi", h1.GetProperty("X-Span-Id").GetString());
        Assert.Equal("trace-multi", h2.GetProperty("X-Trace-Id").GetString());
        Assert.Equal("span-multi", h2.GetProperty("X-Span-Id").GetString());
    }

    [Fact]
    public async Task GlobalPropagation_InjectsPartialHeaders_WhenPartialContextPresent()
    {
        await using var app = await CreateWithGlobalPropagationAsync();

        var json = await app.GetJsonAsync("/propagate", ("X-Trace-Id", "trace-partial"));

        var propagated = json.GetProperty("propagatedHeaders");
        Assert.Equal("trace-partial", propagated.GetProperty("X-Trace-Id").GetString());
        Assert.False(propagated.TryGetProperty("X-Span-Id", out _));
    }

    // ──────────────────────────────────────────────────────────────
    //  Per-Client HTTP Propagation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task PerClientPropagation_ConfiguredClient_InjectsContextHeaders()
    {
        await using var app = await CreateWithPerClientPropagationAsync();

        var json = await app.GetJsonAsync("/propagate/named/with-handler",
            ("X-Trace-Id", "per-client-trace"), ("X-Span-Id", "per-client-span"));

        var propagated = json.GetProperty("propagatedHeaders");
        Assert.Equal("per-client-trace", propagated.GetProperty("X-Trace-Id").GetString());
        Assert.Equal("per-client-span", propagated.GetProperty("X-Span-Id").GetString());
    }

    [Fact]
    public async Task PerClientPropagation_UnconfiguredClient_DoesNotInjectHeaders()
    {
        await using var app = await CreateWithPerClientPropagationAsync();

        var json = await app.GetJsonAsync("/propagate/named/without-handler",
            ("X-Trace-Id", "should-not-propagate"));

        var extracted = json.GetProperty("extracted");
        Assert.Equal("should-not-propagate", extracted.GetProperty("traceId").GetString());

        var propagated = json.GetProperty("propagatedHeaders");
        Assert.False(propagated.TryGetProperty("X-Trace-Id", out _));
    }

    // ──────────────────────────────────────────────────────────────
    //  Multiple Context Types
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task MultipleContextTypes_AllExtractedAndPropagated()
    {
        await using var app = await CreateWithMultipleContextTypesAsync();

        var json = await app.GetJsonAsync("/propagate/all",
            ("X-Trace-Id", "trace-1"), ("X-Span-Id", "span-1"),
            ("X-Tenant-Id", "acme"), ("X-Region", "us-east-1"));

        var correlation = json.GetProperty("correlation");
        Assert.Equal("trace-1", correlation.GetProperty("traceId").GetString());
        Assert.Equal("span-1", correlation.GetProperty("spanId").GetString());

        var tenant = json.GetProperty("tenant");
        Assert.Equal("acme", tenant.GetProperty("tenantId").GetString());
        Assert.Equal("us-east-1", tenant.GetProperty("region").GetString());

        var propagated = json.GetProperty("propagatedHeaders");
        Assert.Equal("trace-1", propagated.GetProperty("X-Trace-Id").GetString());
        Assert.Equal("span-1", propagated.GetProperty("X-Span-Id").GetString());
        Assert.Equal("acme", propagated.GetProperty("X-Tenant-Id").GetString());
        Assert.Equal("us-east-1", propagated.GetProperty("X-Region").GetString());
    }

    [Fact]
    public async Task MultipleContextTypes_PartialHeaders_OnlyAvailableTypesExtracted()
    {
        await using var app = await CreateWithMultipleContextTypesAsync();

        var json = await app.GetJsonAsync("/propagate/all",
            ("X-Tenant-Id", "acme-only"));

        var correlation = json.GetProperty("correlation");
        AssertJsonStringOrNull(correlation, "traceId", null);

        var tenant = json.GetProperty("tenant");
        Assert.Equal("acme-only", tenant.GetProperty("tenantId").GetString());

        var propagated = json.GetProperty("propagatedHeaders");
        Assert.False(propagated.TryGetProperty("X-Trace-Id", out _));
        Assert.Equal("acme-only", propagated.GetProperty("X-Tenant-Id").GetString());
    }

    // ──────────────────────────────────────────────────────────────
    //  Domain-Scoped Context
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DomainScoped_MiddlewareWritesToDomain_HandlerReadsFromDomain()
    {
        await using var app = await CreateWithDomainContextAsync();

        var json = await app.GetJsonAsync("/propagate?domain=web-api",
            ("X-Trace-Id", "domain-trace"), ("X-Span-Id", "domain-span"));

        var extracted = json.GetProperty("extracted");
        Assert.Equal("domain-trace", extracted.GetProperty("traceId").GetString());
        Assert.Equal("domain-span", extracted.GetProperty("spanId").GetString());

        var propagated = json.GetProperty("propagatedHeaders");
        Assert.Equal("domain-trace", propagated.GetProperty("X-Trace-Id").GetString());
        Assert.Equal("domain-span", propagated.GetProperty("X-Span-Id").GetString());
    }

    [Fact]
    public async Task DomainScoped_DefaultDomain_DoesNotReceiveContext()
    {
        await using var app = await CreateWithDomainContextAsync();

        var json = await app.GetJsonAsync("/context",
            ("X-Trace-Id", "domain-only-trace"));

        AssertJsonStringOrNull(json, "traceId", null);
        AssertJsonStringOrNull(json, "spanId", null);
    }

    // ──────────────────────────────────────────────────────────────
    //  Concurrent Request Isolation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentRequests_EachRequestMaintainsOwnContext()
    {
        await using var app = await CreateWithGlobalPropagationAsync();

        var tasks = Enumerable.Range(1, 50).Select(async i =>
        {
            var traceId = $"trace-{i}";
            var spanId = $"span-{i}";

            var json = await app.GetJsonAsync("/propagate",
                ("X-Trace-Id", traceId), ("X-Span-Id", spanId));

            var extracted = json.GetProperty("extracted");
            var propagated = json.GetProperty("propagatedHeaders");

            return new
            {
                Index = i,
                ExpectedTrace = traceId,
                ExtractedTrace = extracted.GetProperty("traceId").GetString(),
                PropagatedTrace = propagated.GetProperty("X-Trace-Id").GetString(),
                ExpectedSpan = spanId,
                PropagatedSpan = propagated.GetProperty("X-Span-Id").GetString()
            };
        });

        var results = await Task.WhenAll(tasks);

        foreach (var r in results)
        {
            Assert.Equal(r.ExpectedTrace, r.ExtractedTrace);
            Assert.Equal(r.ExpectedTrace, r.PropagatedTrace);
            Assert.Equal(r.ExpectedSpan, r.PropagatedSpan);
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Full Round-Trip
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task FullRoundTrip_IncomingHeaders_ArePreservedInOutgoingRequest()
    {
        await using var app = await CreateWithGlobalPropagationAsync();

        var traceId = Guid.NewGuid().ToString();
        var spanId = Guid.NewGuid().ToString();

        var json = await app.GetJsonAsync("/propagate",
            ("X-Trace-Id", traceId), ("X-Span-Id", spanId));

        var extracted = json.GetProperty("extracted");
        Assert.Equal(traceId, extracted.GetProperty("traceId").GetString());
        Assert.Equal(spanId, extracted.GetProperty("spanId").GetString());

        var propagated = json.GetProperty("propagatedHeaders");
        Assert.Equal(traceId, propagated.GetProperty("X-Trace-Id").GetString());
        Assert.Equal(spanId, propagated.GetProperty("X-Span-Id").GetString());
    }

    [Fact]
    public async Task FullRoundTrip_MultipleContextTypes_AllSurvivePipeline()
    {
        await using var app = await CreateWithMultipleContextTypesAsync();

        var traceId = Guid.NewGuid().ToString();
        var tenantId = "tenant-" + Guid.NewGuid().ToString("N")[..8];
        var region = "eu-west-2";

        var json = await app.GetJsonAsync("/propagate/all",
            ("X-Trace-Id", traceId), ("X-Tenant-Id", tenantId), ("X-Region", region));

        var propagated = json.GetProperty("propagatedHeaders");
        Assert.Equal(traceId, propagated.GetProperty("X-Trace-Id").GetString());
        Assert.Equal(tenantId, propagated.GetProperty("X-Tenant-Id").GetString());
        Assert.Equal(region, propagated.GetProperty("X-Region").GetString());
    }

    // ──────────────────────────────────────────────────────────────
    //  Server Factory Methods
    // ──────────────────────────────────────────────────────────────

    private static Task<TestApp> CreateWithGlobalPropagationAsync()
    {
        return TestApp.CreateAsync(builder =>
        {
            builder.Services.AddContextR(ctx => ctx
                .Add<CorrelationContext>(reg => reg
                    .MapProperty(c => c.TraceId, "X-Trace-Id")
                    .MapProperty(c => c.SpanId, "X-Span-Id")
                    .UseAspNetCore()
                    .UseGlobalHttpPropagation()));

            RegisterCaptureClients(builder.Services, "backend", "backend-1", "backend-2");
        });
    }

    private static Task<TestApp> CreateWithPerClientPropagationAsync()
    {
        return TestApp.CreateAsync(builder =>
        {
            builder.Services.AddContextR(ctx => ctx
                .Add<CorrelationContext>(reg => reg
                    .MapProperty(c => c.TraceId, "X-Trace-Id")
                    .MapProperty(c => c.SpanId, "X-Span-Id")
                    .UseAspNetCore()));

            builder.Services.AddHttpClient("with-handler", c => c.BaseAddress = new Uri("http://captured-backend/"))
                .AddContextRHandler<CorrelationContext>()
                .ConfigurePrimaryHttpMessageHandler(() => new HeaderCaptureHandler());

            builder.Services.AddHttpClient("without-handler", c => c.BaseAddress = new Uri("http://captured-backend/"))
                .ConfigurePrimaryHttpMessageHandler(() => new HeaderCaptureHandler());
        });
    }

    private static Task<TestApp> CreateWithMultipleContextTypesAsync()
    {
        return TestApp.CreateAsync(builder =>
        {
            builder.Services.AddContextR(ctx =>
            {
                ctx.Add<CorrelationContext>(reg => reg
                    .MapProperty(c => c.TraceId, "X-Trace-Id")
                    .MapProperty(c => c.SpanId, "X-Span-Id")
                    .UseAspNetCore()
                    .UseGlobalHttpPropagation());

                ctx.Add<TenantContext>(reg => reg
                    .MapProperty(c => c.TenantId, "X-Tenant-Id")
                    .MapProperty(c => c.Region, "X-Region")
                    .UseAspNetCore()
                    .UseGlobalHttpPropagation());
            });

            RegisterCaptureClients(builder.Services, "backend");
        });
    }

    private static Task<TestApp> CreateWithDomainContextAsync()
    {
        return TestApp.CreateAsync(builder =>
        {
            builder.Services.AddContextR(ctx =>
            {
                ctx.Add<CorrelationContext>()
                   .AddDomain("web-api", d => d.Add<CorrelationContext>(reg => reg
                       .MapProperty(c => c.TraceId, "X-Trace-Id")
                       .MapProperty(c => c.SpanId, "X-Span-Id")
                       .UseAspNetCore()
                       .UseGlobalHttpPropagation()));
            });

            RegisterCaptureClients(builder.Services, "backend");
        });
    }

    // ──────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────

    private static void RegisterCaptureClients(IServiceCollection services, params string[] names)
    {
        foreach (var name in names)
        {
            services.AddHttpClient(name, c => c.BaseAddress = new Uri("http://captured-backend/"))
                .ConfigurePrimaryHttpMessageHandler(() => new HeaderCaptureHandler());
        }
    }

    private static (string Key, string Value)[] BuildHeaders(
        params (string Key, string? Value)[] candidates)
    {
        return candidates
            .Where(h => h.Value is not null)
            .Select(h => (h.Key, h.Value!))
            .ToArray();
    }

    private static void AssertJsonStringOrNull(
        JsonElement element, string propertyName, string? expected)
    {
        var prop = element.GetProperty(propertyName);

        if (expected is null)
            Assert.True(prop.ValueKind == JsonValueKind.Null,
                $"Expected '{propertyName}' to be null but was '{prop}'");
        else
            Assert.Equal(expected, prop.GetString());
    }
}
