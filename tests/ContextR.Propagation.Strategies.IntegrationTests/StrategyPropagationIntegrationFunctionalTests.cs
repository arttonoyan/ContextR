using System.Text.Json;
using ContextR.Propagation.Chunking;
using ContextR.Propagation.Strategies.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Propagation.Strategies.IntegrationTests;

public sealed class StrategyPropagationIntegrationFunctionalTests
{
    [Fact]
    public async Task Integration_InlineJson_ExtractsComplexContext_FromIncomingHeaders()
    {
        await using var app = await CreateWithInlineJsonAsync(
            maxPayloadBytes: 4096,
            oversizeBehavior: ContextOversizeBehavior.FailFast);

        var json = await app.GetJsonAsync(
            "/context/complex",
            ("X-Trace-Id", "trace-in"),
            ("X-Tags", "[\"a\",\"b\",\"c\"]"),
            ("X-Payload", "{\"code\":\"payload-in\"}"));

        Assert.True(json.GetProperty("hasContext").GetBoolean());
        Assert.Equal("trace-in", json.GetProperty("traceId").GetString());
        Assert.Equal(3, json.GetProperty("tags").GetArrayLength());
        Assert.Equal("payload-in", json.GetProperty("payloadCode").GetString());
    }

    [Fact]
    public async Task Integration_InlineJson_PropagatesComplexContext_ToOutgoingHeaders()
    {
        await using var app = await CreateWithInlineJsonAsync(
            maxPayloadBytes: 4096,
            oversizeBehavior: ContextOversizeBehavior.FailFast);

        var json = await app.GetJsonAsync("/propagate/complex/manual");
        var headers = json.GetProperty("propagatedHeaders");

        Assert.Equal("trace-1", headers.GetProperty("X-Trace-Id").GetString());

        var tagsHeader = headers.GetProperty("X-Tags").GetString();
        Assert.NotNull(tagsHeader);
        Assert.Equal(["a", "b"], JsonSerializer.Deserialize<List<string>>(tagsHeader!));

        var payloadHeader = headers.GetProperty("X-Payload").GetString();
        Assert.NotNull(payloadHeader);
        Assert.Contains("\"code\":\"payload-1\"", payloadHeader!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Functional_OversizeFailFast_Inject_ThrowsDeterministicError()
    {
        await using var app = await CreateWithInlineJsonAsync(
            maxPayloadBytes: 64,
            oversizeBehavior: ContextOversizeBehavior.FailFast);

        var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            app.GetRawAsync("/propagate/complex/oversize"));

        Assert.Contains("exceeded limit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Functional_OversizeFailFast_Extract_DropsOversizeField()
    {
        await using var app = await CreateWithInlineJsonAsync(
            maxPayloadBytes: 32,
            oversizeBehavior: ContextOversizeBehavior.FailFast);

        var oversizedPayload = JsonSerializer.Serialize(
            Enumerable.Range(1, 20).Select(i => $"value-{i}").ToList());

        var json = await app.GetJsonAsync("/context/complex", ("X-Tags", oversizedPayload));

        Assert.False(json.GetProperty("hasContext").GetBoolean());
    }

    [Fact]
    public async Task Integration_OversizeSkipProperty_Inject_PropagatesRemainingProperties()
    {
        await using var app = await CreateWithInlineJsonAsync(
            maxPayloadBytes: 64,
            oversizeBehavior: ContextOversizeBehavior.SkipProperty);

        var json = await app.GetJsonAsync("/propagate/complex/oversize");
        var headers = json.GetProperty("propagatedHeaders");

        Assert.Equal("trace-oversize", headers.GetProperty("X-Trace-Id").GetString());
        Assert.False(headers.TryGetProperty("X-Tags", out _));
        Assert.False(headers.TryGetProperty("X-Payload", out _));
    }

    [Fact]
    public async Task Integration_OversizeSkipProperty_Extract_DropsOversizeField()
    {
        await using var app = await CreateWithInlineJsonAsync(
            maxPayloadBytes: 32,
            oversizeBehavior: ContextOversizeBehavior.SkipProperty);

        var oversizedPayload = JsonSerializer.Serialize(
            Enumerable.Range(1, 20).Select(i => $"value-{i}").ToList());

        var json = await app.GetJsonAsync(
            "/context/complex",
            ("X-Trace-Id", "trace-skip"),
            ("X-Tags", oversizedPayload));

        Assert.True(json.GetProperty("hasContext").GetBoolean());
        Assert.Equal("trace-skip", json.GetProperty("traceId").GetString());
        Assert.True(json.GetProperty("tags").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Functional_FallbackToToken_Inject_ThrowsDeterministicErrorWhenNoTokenStrategy()
    {
        await using var app = await CreateWithInlineJsonAsync(
            maxPayloadBytes: 64,
            oversizeBehavior: ContextOversizeBehavior.FallbackToToken);

        var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            app.GetRawAsync("/propagate/complex/oversize"));

        Assert.Contains("requested token fallback", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Functional_FallbackToToken_Extract_DropsOversizeFieldWhenNoTokenStrategy()
    {
        await using var app = await CreateWithInlineJsonAsync(
            maxPayloadBytes: 64,
            oversizeBehavior: ContextOversizeBehavior.FallbackToToken);

        var oversizedPayload = JsonSerializer.Serialize(
            Enumerable.Range(1, 20).Select(i => $"value-{i}").ToList());

        var json = await app.GetJsonAsync("/context/complex", ("X-Tags", oversizedPayload));
        Assert.False(json.GetProperty("hasContext").GetBoolean());
    }

    [Fact]
    public async Task Integration_ChunkProperty_Inject_SplitsOversizePayloadIntoDerivedHeaders()
    {
        await using var app = await CreateWithInlineJsonAsync(
            maxPayloadBytes: 64,
            oversizeBehavior: ContextOversizeBehavior.ChunkProperty);

        var json = await app.GetJsonAsync("/propagate/complex/oversize");
        var headers = json.GetProperty("propagatedHeaders");

        Assert.Equal("trace-oversize", headers.GetProperty("X-Trace-Id").GetString());
        Assert.True(headers.TryGetProperty("X-Tags__chunks", out _));
        Assert.Contains(headers.EnumerateObject(), p => p.Name.StartsWith("X-Tags__chunk_", StringComparison.Ordinal));
        Assert.True(headers.TryGetProperty("X-Payload__chunks", out _));
    }

    [Fact]
    public async Task Integration_ChunkProperty_Extract_ReassemblesOversizePayload()
    {
        await using var app = await CreateWithInlineJsonAsync(
            maxPayloadBytes: 32,
            oversizeBehavior: ContextOversizeBehavior.ChunkProperty);

        var oversizedPayload = JsonSerializer.Serialize(
            Enumerable.Range(1, 20).Select(i => $"value-{i}").ToList());

        var chunkCarrier = new Dictionary<string, string>();
        var bytes = System.Text.Encoding.UTF8.GetBytes(oversizedPayload);
        var chunkSize = 32;
        var index = 0;
        var offset = 0;
        while (offset < bytes.Length)
        {
            var count = Math.Min(chunkSize, bytes.Length - offset);
            var part = System.Text.Encoding.UTF8.GetString(bytes, offset, count);
            chunkCarrier[$"X-Tags__chunk_{index++}"] = part;
            offset += count;
        }

        chunkCarrier["X-Tags__chunks"] = index.ToString();
        chunkCarrier["X-Trace-Id"] = "trace-chunk";

        var headers = chunkCarrier.Select(kv => (kv.Key, kv.Value)).ToArray();
        var json = await app.GetJsonAsync("/context/complex", headers);

        Assert.True(json.GetProperty("hasContext").GetBoolean());
        Assert.Equal("trace-chunk", json.GetProperty("traceId").GetString());
        Assert.Equal(20, json.GetProperty("tags").GetArrayLength());
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
                    .UseChunkingPayloads<ComplexContext>()
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
}
