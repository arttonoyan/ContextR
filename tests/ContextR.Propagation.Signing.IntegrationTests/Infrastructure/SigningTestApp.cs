using System.Net.Http.Json;
using System.Text.Json;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ContextR.Propagation.Signing.IntegrationTests.Infrastructure;

internal sealed class SigningTestApp : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly HttpClient _client;

    private SigningTestApp(WebApplication app)
    {
        _app = app;
        _client = app.GetTestServer().CreateClient();
    }

    public static async Task<SigningTestApp> CreateAsync(Action<WebApplicationBuilder> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        configure(builder);

        var app = builder.Build();
        MapEndpoints(app);
        await app.StartAsync();

        return new SigningTestApp(app);
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
        app.MapGet("/context", (IContextAccessor accessor) =>
        {
            var ctx = accessor.GetContext<TenantContext>();
            return Results.Json(new
            {
                HasContext = ctx is not null,
                ctx?.TenantId,
                ctx?.Region
            });
        });

        app.MapGet("/propagate", async (IContextWriter writer, IHttpClientFactory factory) =>
        {
            writer.SetContext(new TenantContext
            {
                TenantId = "acme",
                Region = "us-east-1"
            });

            using var client = factory.CreateClient("backend");
            using var response = await client.GetAsync("/probe");
            var outgoing = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return Results.Json(new { PropagatedHeaders = outgoing });
        });
    }
}

internal sealed class TenantContext
{
    public string? TenantId { get; set; }
    public string? Region { get; set; }
}
