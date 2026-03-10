using System.Security.Cryptography;
using System.Text.Json;
using ContextR.Propagation.Signing.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Propagation.Signing.IntegrationTests;

public sealed class SigningHttpPropagationTests
{
    private static readonly byte[] TestKey = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public async Task Propagate_IncludesSignatureHeader_InOutgoingRequest()
    {
        await using var app = await CreateTestAppAsync();

        var json = await app.GetJsonAsync("/propagate");
        var headers = json.GetProperty("propagatedHeaders");

        Assert.True(headers.TryGetProperty("X-Context-Signature", out var sig));
        Assert.Contains(".", sig.GetString()!);
        Assert.True(headers.TryGetProperty("X-Tenant-Id", out _));
        Assert.True(headers.TryGetProperty("X-Region", out _));
    }

    [Fact]
    public async Task Extract_ValidSignedHeaders_RestoresContext()
    {
        await using var app = await CreateTestAppAsync();

        var propagator = BuildStandalonePropagator();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        propagator.Inject(
            new TenantContext { TenantId = "acme", Region = "eu-west-1" },
            headers,
            static (dict, key, value) => dict[key] = value);

        var requestHeaders = headers.Select(kv => (kv.Key, kv.Value)).ToArray();
        var json = await app.GetJsonAsync("/context", requestHeaders);

        Assert.True(json.GetProperty("hasContext").GetBoolean());
        Assert.Equal("acme", json.GetProperty("tenantId").GetString());
        Assert.Equal("eu-west-1", json.GetProperty("region").GetString());
    }

    [Fact]
    public async Task Extract_TamperedHeaders_ReturnsNoContext()
    {
        await using var app = await CreateTestAppAsync();

        var propagator = BuildStandalonePropagator();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        propagator.Inject(
            new TenantContext { TenantId = "acme", Region = "eu-west-1" },
            headers,
            static (dict, key, value) => dict[key] = value);

        headers["X-Tenant-Id"] = "evil-corp";

        var requestHeaders = headers.Select(kv => (kv.Key, kv.Value)).ToArray();
        var json = await app.GetJsonAsync("/context", requestHeaders);

        Assert.False(json.GetProperty("hasContext").GetBoolean());
    }

    [Fact]
    public async Task Extract_MissingSignature_ReturnsNoContext()
    {
        await using var app = await CreateTestAppAsync();

        var json = await app.GetJsonAsync("/context",
            ("X-Tenant-Id", "acme"),
            ("X-Region", "us-east-1"));

        Assert.False(json.GetProperty("hasContext").GetBoolean());
    }

    private static Task<SigningTestApp> CreateTestAppAsync()
    {
        return SigningTestApp.CreateAsync(builder =>
        {
            builder.Services.AddContextR(ctx =>
            {
                ctx.Add<TenantContext>(reg => reg
                    .MapProperty(c => c.TenantId, "X-Tenant-Id")
                    .MapProperty(c => c.Region, "X-Region")
                    .UseContextSigning<TenantContext>(o => o.Key = TestKey)
                    .OnPropagationFailure<TenantContext>(_ => PropagationFailureAction.SkipContext)
                    .UseAspNetCore()
                    .UseGlobalHttpPropagation());
            });

            builder.Services.AddHttpClient("backend", c => c.BaseAddress = new Uri("http://captured-backend/"))
                .ConfigurePrimaryHttpMessageHandler(() => new HeaderCaptureHandler());
        });
    }

    private static IContextPropagator<TenantContext> BuildStandalonePropagator()
    {
        var services = new ServiceCollection();

        services.AddContextR(ctx =>
        {
            ctx.Add<TenantContext>(reg => reg
                .MapProperty(c => c.TenantId, "X-Tenant-Id")
                .MapProperty(c => c.Region, "X-Region")
                .UseContextSigning<TenantContext>(o => o.Key = TestKey));
        });

        return services.BuildServiceProvider().GetRequiredService<IContextPropagator<TenantContext>>();
    }
}
