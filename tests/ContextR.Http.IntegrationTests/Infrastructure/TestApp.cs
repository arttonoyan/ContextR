using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace ContextR.Http.IntegrationTests.Infrastructure;

/// <summary>
/// A self-contained test server that wraps <see cref="WebApplication"/> with <see cref="TestServer"/>.
/// Provides a preconfigured <see cref="HttpClient"/> and common test endpoints.
/// <para>
/// Usage:
/// <code>
/// await using var app = await TestApp.CreateAsync(builder =&gt;
/// {
///     builder.Services.AddContextR(ctx =&gt; { /* ... */ });
///     builder.Services.AddHttpClient("backend")
///         .ConfigurePrimaryHttpMessageHandler(() =&gt; new HeaderCaptureHandler());
/// });
///
/// var json = await app.GetJsonAsync("/context", ("X-Trace-Id", "abc"));
/// </code>
/// </para>
/// </summary>
internal sealed class TestApp : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly HttpClient _client;

    private TestApp(WebApplication app)
    {
        _app = app;
        _client = app.GetTestServer().CreateClient();
    }

    /// <summary>
    /// Creates a fully configured test application. The <paramref name="configure"/>
    /// callback is where you register ContextR, HttpClients, and any other services.
    /// Standard test endpoints are mapped automatically.
    /// </summary>
    public static async Task<TestApp> CreateAsync(Action<WebApplicationBuilder> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        configure(builder);

        var app = builder.Build();
        MapEndpoints(app);
        await app.StartAsync();

        return new TestApp(app);
    }

    /// <summary>
    /// Sends a GET request with optional headers and deserializes the response as <see cref="JsonElement"/>.
    /// </summary>
    public async Task<JsonElement> GetJsonAsync(string path, params (string Key, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        foreach (var (key, value) in headers)
            request.Headers.TryAddWithoutValidation(key, value);

        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/context", (HttpContext http, IContextAccessor accessor) =>
        {
            var domain = http.Request.Query["domain"].FirstOrDefault();
            var ctx = domain is not null
                ? accessor.GetContext<CorrelationContext>(domain)
                : accessor.GetContext<CorrelationContext>();

            return Results.Json(new { ctx?.TraceId, ctx?.SpanId });
        });

        app.MapGet("/propagate", async (HttpContext http, IContextAccessor accessor, IHttpClientFactory factory) =>
        {
            var domain = http.Request.Query["domain"].FirstOrDefault();
            var ctx = domain is not null
                ? accessor.GetContext<CorrelationContext>(domain)
                : accessor.GetContext<CorrelationContext>();

            using var client = factory.CreateClient("backend");
            using var resp = await client.GetAsync("/probe");
            var outgoing = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();

            return Results.Json(new
            {
                Extracted = new { ctx?.TraceId, ctx?.SpanId },
                PropagatedHeaders = outgoing
            });
        });

        app.MapGet("/propagate/multi", async (IHttpClientFactory factory) =>
        {
            using var c1 = factory.CreateClient("backend-1");
            using var c2 = factory.CreateClient("backend-2");
            using var r1 = await c1.GetAsync("/probe");
            using var r2 = await c2.GetAsync("/probe");

            return Results.Json(new
            {
                Client1Headers = await r1.Content.ReadFromJsonAsync<Dictionary<string, string>>(),
                Client2Headers = await r2.Content.ReadFromJsonAsync<Dictionary<string, string>>()
            });
        });

        app.MapGet("/propagate/named/{name}", async (string name, IContextAccessor accessor, IHttpClientFactory factory) =>
        {
            var ctx = accessor.GetContext<CorrelationContext>();
            using var client = factory.CreateClient(name);
            using var resp = await client.GetAsync("/probe");
            var outgoing = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();

            return Results.Json(new
            {
                Extracted = new { ctx?.TraceId, ctx?.SpanId },
                PropagatedHeaders = outgoing
            });
        });

        app.MapGet("/propagate/all", async (IContextAccessor accessor, IHttpClientFactory factory) =>
        {
            var corr = accessor.GetContext<CorrelationContext>();
            var tenant = accessor.GetContext<TenantContext>();
            using var client = factory.CreateClient("backend");
            using var resp = await client.GetAsync("/probe");
            var outgoing = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();

            return Results.Json(new
            {
                Correlation = new { corr?.TraceId, corr?.SpanId },
                Tenant = new { tenant?.TenantId, tenant?.Region },
                PropagatedHeaders = outgoing
            });
        });

        app.MapGet("/context/list", (IContextAccessor accessor) =>
        {
            var ctx = accessor.GetContext<ListPropagationContext>();
            return Results.Json(new
            {
                HasContext = ctx is not null,
                TagCount = ctx?.Tags?.Count
            });
        });

        app.MapGet("/context/class", (IContextAccessor accessor) =>
        {
            var ctx = accessor.GetContext<ClassPropagationContext>();
            return Results.Json(new
            {
                HasContext = ctx is not null,
                PayloadCode = ctx?.Payload?.Code
            });
        });

        app.MapGet("/propagate/list/manual", async (IContextWriter writer, IHttpClientFactory factory) =>
        {
            writer.SetContext(new ListPropagationContext { Tags = ["a", "b"] });
            using var client = factory.CreateClient("backend");
            using var resp = await client.GetAsync("/probe");
            var outgoing = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return Results.Json(new { PropagatedHeaders = outgoing });
        });

        app.MapGet("/propagate/class/manual", async (IContextWriter writer, IHttpClientFactory factory) =>
        {
            writer.SetContext(new ClassPropagationContext
            {
                Payload = new PayloadValue { Code = "payload-1" }
            });

            using var client = factory.CreateClient("backend");
            using var resp = await client.GetAsync("/probe");
            var outgoing = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return Results.Json(new { PropagatedHeaders = outgoing });
        });
    }
}

public class CorrelationContext
{
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
}

public class TenantContext
{
    public string? TenantId { get; set; }
    public string? Region { get; set; }
}

public class ListPropagationContext
{
    public List<string>? Tags { get; set; }
}

public class ClassPropagationContext
{
    public PayloadValue? Payload { get; set; }
}

public class PayloadValue
{
    public string Code { get; set; } = string.Empty;

    public override string ToString() => Code;
}
