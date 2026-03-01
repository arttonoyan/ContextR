using System.Text.Json;
using ContextR.Propagation.Chunking;
using ContextR.Propagation.Strategies.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Propagation.Strategies.IntegrationTests;

public sealed class StrategyPropagationAdvancedIntegrationTests
{
    private static readonly string[] value = ["r1", "r2"];

    [Fact]
    public async Task Integration_InlineJson_FromCurrentContext_RoundTripsComplexHeaders()
    {
        await using var app = await CreateWithInlineJsonAsync(4096, ContextOversizeBehavior.FailFast);

        var tags = JsonSerializer.Serialize(value);
        var payload = JsonSerializer.Serialize(new { code = "payload-r1" });
        var json = await app.GetJsonAsync(
            "/propagate/complex/from-current",
            ("X-Trace-Id", "trace-r1"),
            ("X-Tags", tags),
            ("X-Payload", payload));

        var extracted = json.GetProperty("extracted");
        Assert.Equal("trace-r1", extracted.GetProperty("traceId").GetString());
        Assert.Equal(2, extracted.GetProperty("tags").GetArrayLength());
        Assert.Equal("payload-r1", extracted.GetProperty("payloadCode").GetString());

        var propagated = json.GetProperty("propagatedHeaders");
        Assert.Equal("trace-r1", propagated.GetProperty("X-Trace-Id").GetString());
        Assert.NotNull(propagated.GetProperty("X-Tags").GetString());
        Assert.NotNull(propagated.GetProperty("X-Payload").GetString());
    }

    [Fact]
    public async Task Integration_RequiredPayload_Throw_ReturnsServerError_OnInvalidJson()
    {
        await using var app = await CreateWithRequiredPayloadFailurePolicyAsync(PropagationFailureAction.Throw);

        var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            app.GetRawAsync(
                "/context/complex",
                ("X-Trace-Id", "trace-throw"),
                ("X-Payload", "not-json")));

        Assert.Contains("ParseFailed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Integration_RequiredPayload_SkipProperty_Continues_WithOtherFields()
    {
        await using var app = await CreateWithRequiredPayloadFailurePolicyAsync(PropagationFailureAction.SkipProperty);

        var json = await app.GetJsonAsync(
            "/context/complex",
            ("X-Trace-Id", "trace-skip-property"),
            ("X-Payload", "not-json"));

        Assert.True(json.GetProperty("hasContext").GetBoolean());
        Assert.Equal("trace-skip-property", json.GetProperty("traceId").GetString());
        Assert.True(json.GetProperty("payloadCode").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Integration_RequiredPayload_SkipContext_DropsContext_WhenRequiredInvalid()
    {
        await using var app = await CreateWithRequiredPayloadFailurePolicyAsync(PropagationFailureAction.SkipContext);

        var json = await app.GetJsonAsync(
            "/context/complex",
            ("X-Trace-Id", "trace-skip-context"),
            ("X-Payload", "not-json"));

        Assert.False(json.GetProperty("hasContext").GetBoolean());
    }

    [Fact]
    public async Task Integration_RequiredPayload_Missing_Throw_ReturnsServerError()
    {
        await using var app = await CreateWithRequiredPayloadFailurePolicyAsync(PropagationFailureAction.Throw);

        var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            app.GetRawAsync(
                "/context/complex",
                ("X-Trace-Id", "trace-missing-required")));

        Assert.Contains("MissingRequired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Integration_RequiredPayload_Missing_SkipProperty_AllowsRemaining()
    {
        await using var app = await CreateWithRequiredPayloadFailurePolicyAsync(PropagationFailureAction.SkipProperty);

        var json = await app.GetJsonAsync(
            "/context/complex",
            ("X-Trace-Id", "trace-missing-skip"));

        Assert.True(json.GetProperty("hasContext").GetBoolean());
        Assert.Equal("trace-missing-skip", json.GetProperty("traceId").GetString());
        Assert.True(json.GetProperty("payloadCode").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Integration_ParallelRoundTrip_RequestsRemainIsolated()
    {
        await using var app = await CreateWithInlineJsonAsync(4096, ContextOversizeBehavior.FailFast);

        var tasks = Enumerable.Range(1, 50).Select(async i =>
        {
            var expectedTrace = $"trace-{i}";
            var expectedPayload = $"payload-{i}";
            var tags = JsonSerializer.Serialize(new[] { $"tag-{i}", $"tag-{i + 1}" });
            var payload = JsonSerializer.Serialize(new { code = expectedPayload });

            var json = await app.GetJsonAsync(
                "/propagate/complex/from-current",
                ("X-Trace-Id", expectedTrace),
                ("X-Tags", tags),
                ("X-Payload", payload));

            var extracted = json.GetProperty("extracted");
            var propagated = json.GetProperty("propagatedHeaders");
            var propagatedTags = JsonSerializer.Deserialize<List<string>>(propagated.GetProperty("X-Tags").GetString()!);
            var propagatedPayload = propagated.GetProperty("X-Payload").GetString()!;

            Assert.Equal(expectedTrace, extracted.GetProperty("traceId").GetString());
            Assert.Equal(expectedPayload, extracted.GetProperty("payloadCode").GetString());
            Assert.Equal(expectedTrace, propagated.GetProperty("X-Trace-Id").GetString());
            Assert.NotNull(propagatedTags);
            Assert.Equal(2, propagatedTags!.Count);
            Assert.Contains($"\"code\":\"{expectedPayload}\"", propagatedPayload, StringComparison.Ordinal);
        });

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task Integration_ParallelMixedOversizeAndNormal_SkipProperty_RemainsStable()
    {
        await using var app = await CreateWithInlineJsonAsync(64, ContextOversizeBehavior.SkipProperty);

        var tasks = Enumerable.Range(1, 40).Select(async i =>
        {
            if (i % 2 == 0)
            {
                var json = await app.GetJsonAsync("/propagate/complex/oversize");
                var headers = json.GetProperty("propagatedHeaders");
                Assert.Equal("trace-oversize", headers.GetProperty("X-Trace-Id").GetString());
                Assert.False(headers.TryGetProperty("X-Tags", out _));
                Assert.False(headers.TryGetProperty("X-Payload", out _));
                return;
            }

            var expectedTrace = $"trace-normal-{i}";
            var expectedPayload = $"payload-normal-{i}";
            var jsonNormal = await app.GetJsonAsync(
                "/propagate/complex/from-current",
                ("X-Trace-Id", expectedTrace),
                ("X-Tags", JsonSerializer.Serialize(new[] { $"n-{i}" })),
                ("X-Payload", JsonSerializer.Serialize(new { code = expectedPayload })));

            var headersNormal = jsonNormal.GetProperty("propagatedHeaders");
            Assert.Equal(expectedTrace, headersNormal.GetProperty("X-Trace-Id").GetString());
            Assert.True(headersNormal.TryGetProperty("X-Tags", out _));
            Assert.Contains(expectedPayload, headersNormal.GetProperty("X-Payload").GetString()!, StringComparison.Ordinal);
        });

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task Integration_HybridPolicy_ContextDefaultSkip_WithPropertyChunkOverride()
    {
        await using var app = await StrategyTestApp.CreateAsync(builder =>
        {
            builder.Services.AddContextR(ctx =>
            {
                ctx.Add<ComplexContext>(reg => reg
                    .UseInlineJsonPayloads(o =>
                    {
                        o.MaxPayloadBytes = 64;
                        o.OversizeBehavior = ContextOversizeBehavior.SkipProperty;
                    })
                    .UseChunkingPayloads()
                    .Map(m => m
                        .DefaultOversizeBehavior(ContextOversizeBehavior.SkipProperty)
                        .Property(c => c.TraceId, "X-Trace-Id").Optional()
                        .Property(c => c.Tags, "X-Tags").OversizeBehavior(ContextOversizeBehavior.ChunkProperty).Optional()
                        .Property(c => c.Payload, "X-Payload").Optional())
                    .UseAspNetCore()
                    .UseGlobalHttpPropagation());
            });

            builder.Services.AddHttpClient("backend", c => c.BaseAddress = new Uri("http://captured-backend/"))
                .ConfigurePrimaryHttpMessageHandler(() => new HeaderCaptureHandler());
        });

        var json = await app.GetJsonAsync("/propagate/complex/oversize");
        var headers = json.GetProperty("propagatedHeaders");

        Assert.Equal("trace-oversize", headers.GetProperty("X-Trace-Id").GetString());
        Assert.True(headers.TryGetProperty("X-Tags__chunks", out _));
        Assert.Contains(headers.EnumerateObject(), h => h.Name.StartsWith("X-Tags__chunk_", StringComparison.Ordinal));
        Assert.False(headers.TryGetProperty("X-Payload", out _));
    }

    private static Task<StrategyTestApp> CreateWithInlineJsonAsync(
        int maxPayloadBytes,
        ContextOversizeBehavior oversizeBehavior)
    {
        return StrategyTestApp.CreateAsync(builder =>
        {
            builder.Services.AddContextR(ctx =>
            {
                ctx.Add<ComplexContext>(reg => reg
                    .UseInlineJsonPayloads<ComplexContext>(o =>
                    {
                        o.MaxPayloadBytes = maxPayloadBytes;
                        o.OversizeBehavior = oversizeBehavior;
                    })
                    .MapProperty(c => c.TraceId, "X-Trace-Id")
                    .MapProperty(c => c.Tags, "X-Tags")
                    .MapProperty(c => c.Payload, "X-Payload")
                    .UseAspNetCore()
                    .UseGlobalHttpPropagation());
            });

            builder.Services.AddHttpClient("backend", c => c.BaseAddress = new Uri("http://captured-backend/"))
                .ConfigurePrimaryHttpMessageHandler(() => new HeaderCaptureHandler());
        });
    }

    private static Task<StrategyTestApp> CreateWithRequiredPayloadFailurePolicyAsync(
        PropagationFailureAction action)
    {
        return StrategyTestApp.CreateAsync(builder =>
        {
            builder.Services.AddContextR(ctx =>
            {
                ctx.Add<ComplexContext>(reg => reg
                    .UseInlineJsonPayloads<ComplexContext>(o =>
                    {
                        o.MaxPayloadBytes = 4096;
                        o.OversizeBehavior = ContextOversizeBehavior.FailFast;
                    })
                    .OnPropagationFailure<ComplexContext>(_ => action)
                    .Map(m => m
                        .Property(c => c.TraceId, "X-Trace-Id").Optional()
                        .Property(c => c.Tags, "X-Tags").Optional()
                        .Property(c => c.Payload, "X-Payload").Required())
                    .UseAspNetCore()
                    .UseGlobalHttpPropagation());
            });

            builder.Services.AddHttpClient("backend", c => c.BaseAddress = new Uri("http://captured-backend/"))
                .ConfigurePrimaryHttpMessageHandler(() => new HeaderCaptureHandler());
        });
    }
}
