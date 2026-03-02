using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace ContextR.AspNetCore.IntegrationTests.Infrastructure;

internal sealed class AspNetCoreIngressTestApp : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly HttpClient _client;

    private AspNetCoreIngressTestApp(WebApplication app)
    {
        _app = app;
        _client = app.GetTestServer().CreateClient();
    }

    public static async Task<AspNetCoreIngressTestApp> CreateAsync(Action<WebApplicationBuilder> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        configure(builder);

        var app = builder.Build();
        MapEndpoints(app);
        await app.StartAsync();
        return new AspNetCoreIngressTestApp(app);
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
        app.MapGet("/ingress/default", (IContextAccessor accessor) =>
        {
            var context = accessor.GetContext<UserContext>();
            return Results.Json(new
            {
                HasContext = context is not null,
                context?.TenantId,
                context?.UserId
            });
        });

        app.MapGet("/ingress/orders", (IContextAccessor accessor) =>
        {
            var context = accessor.GetContext<UserContext>("orders");
            return Results.Json(new
            {
                HasContext = context is not null,
                context?.TenantId,
                context?.UserId
            });
        });
    }
}

internal sealed class UserContext
{
    public string? TenantId { get; set; }
    public string? UserId { get; set; }
}

internal sealed class FeatureFlags
{
    public bool StrictIngress { get; set; }
}

internal sealed class FailureRecorder
{
    public int Invocations { get; private set; }
    public ContextIngressFailureReason? LastReason { get; private set; }
    public string? LastDomain { get; private set; }
    public Type? LastContextType { get; private set; }

    public ContextIngressFailureDecision RecordAndContinue(ContextIngressFailureContext<UserContext> failure)
    {
        Invocations++;
        LastReason = failure.Reason;
        LastDomain = failure.Domain;
        LastContextType = failure.ContextType;
        return ContextIngressFailureDecision.Continue();
    }
}
