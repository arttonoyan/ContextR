using System.Net.Http.Json;
using System.Text.Json;
using ContextR.Grpc.IntegrationTests.Protos;
using global::Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ContextR.Grpc.IntegrationTests.Infrastructure;

internal sealed class GrpcTestCluster : IAsyncDisposable
{
    private readonly WebApplication _backend;
    private readonly WebApplication _frontend;
    private readonly HttpClient _frontendClient;

    private GrpcTestCluster(WebApplication backend, WebApplication frontend)
    {
        _backend = backend;
        _frontend = frontend;
        _frontendClient = frontend.GetTestServer().CreateClient();
    }

    public static async Task<GrpcTestCluster> CreateAsync()
    {
        var backend = await CreateBackendAsync();
        var frontend = await CreateFrontendAsync(backend);
        return new GrpcTestCluster(backend, frontend);
    }

    public async Task<JsonElement> GetRelayJsonAsync(params (string Key, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/relay");
        foreach (var (key, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }

        using var response = await _frontendClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task<JsonElement> GetRelayComplexJsonAsync()
    {
        using var response = await _frontendClient.GetAsync("/relay/complex");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public GrpcProbe.GrpcProbeClient CreateBackendDirectClient()
    {
        var channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions
            {
                HttpClient = _backend.GetTestServer().CreateClient()
            });

        return new GrpcProbe.GrpcProbeClient(channel);
    }

    public async ValueTask DisposeAsync()
    {
        _frontendClient.Dispose();
        await _frontend.DisposeAsync();
        await _backend.DisposeAsync();
    }

    private static async Task<WebApplication> CreateBackendAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddContextR(ctx =>
        {
            ctx.Add<CorrelationContext>(reg => reg
                .MapProperty(c => c.TraceId, "x-trace-id")
                .MapProperty(c => c.SpanId, "x-span-id"));

            ctx.Add<ListPropagationContext>(reg => reg
                .MapProperty(c => c.Tags, "x-tags"));

            ctx.Add<ClassPropagationContext>(reg => reg
                .MapProperty(c => c.Payload, "x-payload"));
        });

        builder.Services.AddGrpc(options =>
        {
            options.Interceptors.Add<ContextInterceptor<CorrelationContext>>();
            options.Interceptors.Add<ContextInterceptor<ListPropagationContext>>();
            options.Interceptors.Add<ContextInterceptor<ClassPropagationContext>>();
        });

        var app = builder.Build();
        app.MapGrpcService<GrpcProbeService>();
        await app.StartAsync();
        return app;
    }

    private static async Task<WebApplication> CreateFrontendAsync(WebApplication backend)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddContextR(ctx =>
        {
            ctx.Add<CorrelationContext>(reg => reg
                .MapProperty(c => c.TraceId, "x-trace-id")
                .MapProperty(c => c.SpanId, "x-span-id")
                .UseGlobalGrpcPropagation());

            ctx.Add<ListPropagationContext>(reg => reg
                .MapProperty(c => c.Tags, "x-tags")
                .UseGlobalGrpcPropagation());

            ctx.Add<ClassPropagationContext>(reg => reg
                .MapProperty(c => c.Payload, "x-payload")
                .UseGlobalGrpcPropagation());
        });

        builder.Services.AddGrpcClient<GrpcProbe.GrpcProbeClient>(options =>
        {
            options.Address = new Uri("http://localhost");
        })
        .ConfigurePrimaryHttpMessageHandler(() => backend.GetTestServer().CreateHandler());

        var app = builder.Build();
        MapFrontendEndpoints(app);
        await app.StartAsync();
        return app;
    }

    private static void MapFrontendEndpoints(WebApplication app)
    {
        app.MapGet("/relay", async (HttpContext http, IContextWriter writer, GrpcProbe.GrpcProbeClient client) =>
        {
            // Reset ambient context for deterministic request-level behavior in tests.
            writer.SetContext<CorrelationContext>(null);
            writer.SetContext<ListPropagationContext>(null);
            writer.SetContext<ClassPropagationContext>(null);

            var trace = http.Request.Headers["x-trace-id"].FirstOrDefault();
            var span = http.Request.Headers["x-span-id"].FirstOrDefault();
            if (trace is not null || span is not null)
            {
                writer.SetContext(new CorrelationContext
                {
                    TraceId = trace,
                    SpanId = span
                });
            }

            var reply = await client.EchoAsync(new ProbeRequest { Message = "relay" });
            return Results.Json(reply);
        });

        app.MapGet("/relay/complex", async (IContextWriter writer, GrpcProbe.GrpcProbeClient client) =>
        {
            writer.SetContext<CorrelationContext>(null);
            writer.SetContext(new ListPropagationContext { Tags = ["a", "b"] });
            writer.SetContext(new ClassPropagationContext
            {
                Payload = new PayloadValue { Code = "payload-1" }
            });

            var reply = await client.EchoAsync(new ProbeRequest { Message = "relay-complex" });
            return Results.Json(reply);
        });
    }
}
