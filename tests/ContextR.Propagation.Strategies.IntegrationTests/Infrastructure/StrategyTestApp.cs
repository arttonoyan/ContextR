using System.Net.Http.Json;
using System.Text.Json;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ContextR.Propagation.Strategies.IntegrationTests.Infrastructure;

internal sealed class StrategyTestApp : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly HttpClient _client;

    private StrategyTestApp(WebApplication app)
    {
        _app = app;
        _client = app.GetTestServer().CreateClient();
    }

    public static async Task<StrategyTestApp> CreateAsync(Action<WebApplicationBuilder> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        configure(builder);

        var app = builder.Build();
        MapEndpoints(app);
        await app.StartAsync();

        return new StrategyTestApp(app);
    }

    public async Task<JsonElement> GetJsonAsync(string path, params (string Key, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        foreach (var (key, value) in headers)
            request.Headers.TryAddWithoutValidation(key, value);

        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task<(HttpStatusCode StatusCode, string Body)> GetRawAsync(
        string path,
        params (string Key, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        foreach (var (key, value) in headers)
            request.Headers.TryAddWithoutValidation(key, value);

        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, body);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/context/complex", (IContextAccessor accessor) =>
        {
            var ctx = accessor.GetContext<ComplexContext>();
            return Results.Json(new
            {
                HasContext = ctx is not null,
                ctx?.TraceId,
                ctx?.Tags,
                PayloadCode = ctx?.Payload?.Code
            });
        });

        app.MapGet("/propagate/complex/manual", async (IContextWriter writer, IHttpClientFactory factory) =>
        {
            writer.SetContext(new ComplexContext
            {
                TraceId = "trace-1",
                Tags = ["a", "b"],
                Payload = new PayloadValue { Code = "payload-1" }
            });

            using var client = factory.CreateClient("backend");
            using var response = await client.GetAsync("/probe");
            var outgoing = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return Results.Json(new { PropagatedHeaders = outgoing });
        });

        app.MapGet("/propagate/complex/from-current", async (IContextAccessor accessor, IHttpClientFactory factory) =>
        {
            var current = accessor.GetContext<ComplexContext>();
            using var client = factory.CreateClient("backend");
            using var response = await client.GetAsync("/probe");
            var outgoing = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

            return Results.Json(new
            {
                Extracted = new
                {
                    current?.TraceId,
                    current?.Tags,
                    PayloadCode = current?.Payload?.Code
                },
                PropagatedHeaders = outgoing
            });
        });

        app.MapGet("/propagate/complex/oversize", async (IContextWriter writer, IHttpClientFactory factory) =>
        {
            writer.SetContext(new ComplexContext
            {
                TraceId = "trace-oversize",
                Tags = Enumerable.Range(0, 50).Select(i => $"item-{i}").ToList(),
                Payload = new PayloadValue { Code = new string('x', 256) }
            });

            using var client = factory.CreateClient("backend");
            using var response = await client.GetAsync("/probe");
            var outgoing = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return Results.Json(new { PropagatedHeaders = outgoing });
        });
    }
}

internal sealed class ComplexContext
{
    public string? TraceId { get; set; }
    public List<string>? Tags { get; set; }
    public PayloadValue? Payload { get; set; }
}

internal sealed class PayloadValue
{
    public string Code { get; set; } = string.Empty;
}
