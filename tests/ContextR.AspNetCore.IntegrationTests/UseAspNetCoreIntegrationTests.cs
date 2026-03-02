using System.Net;
using ContextR.AspNetCore.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.AspNetCore.IntegrationTests;

public sealed class UseAspNetCoreIntegrationTests
{
    [Fact]
    public async Task UseAspNetCore_Default_ExtractsContextFromHeaders()
    {
        await using var app = await AspNetCoreIngressTestApp.CreateAsync(builder =>
        {
            builder.Services.AddContextR(ctx => ctx
                .Add<UserContext>(reg => reg
                    .MapProperty(c => c.TenantId, "X-Tenant-Id")
                    .MapProperty(c => c.UserId, "X-User-Id")
                    .UseAspNetCore()));
        });

        var json = await app.GetJsonAsync("/ingress/default",
            ("X-Tenant-Id", "acme"),
            ("X-User-Id", "u-42"));

        Assert.True(json.GetProperty("hasContext").GetBoolean());
        Assert.Equal("acme", json.GetProperty("tenantId").GetString());
        Assert.Equal("u-42", json.GetProperty("userId").GetString());
    }

    [Fact]
    public async Task UseAspNetCore_OptionsAction_FailRequest_BlocksWhenRequiredMissing()
    {
        await using var app = await AspNetCoreIngressTestApp.CreateAsync(builder =>
        {
            builder.Services.AddContextR(ctx => ctx
                .Add<UserContext>(reg => reg
                    .Map(m => m.Property(c => c.TenantId, "X-Tenant-Id").Required())
                    .UseAspNetCore(o => o.Enforcement(e =>
                    {
                        e.Mode = ContextIngressEnforcementMode.FailRequest;
                    }))));
        });

        var (status, body) = await app.GetRawAsync("/ingress/default");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("validation failed", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UseAspNetCore_OptionsAction_ObserveOnly_ContinuesAndInvokesFailureCallback()
    {
        var recorder = new FailureRecorder();
        await using var app = await AspNetCoreIngressTestApp.CreateAsync(builder =>
        {
            builder.Services.AddSingleton(recorder);
            builder.Services.AddContextR(ctx => ctx
                .Add<UserContext>(reg => reg
                    .Map(m => m.Property(c => c.TenantId, "X-Tenant-Id").Required())
                    .UseAspNetCore(o => o.Enforcement(e =>
                    {
                        e.Mode = ContextIngressEnforcementMode.ObserveOnly;
                        e.OnFailure = recorder.RecordAndContinue;
                    }))));
        });

        var json = await app.GetJsonAsync("/ingress/default");

        Assert.False(json.GetProperty("hasContext").GetBoolean());
        Assert.Equal(1, recorder.Invocations);
        Assert.Equal(ContextIngressFailureReason.ExtractionFailed, recorder.LastReason);
        Assert.Equal(typeof(UserContext), recorder.LastContextType);
    }

    [Fact]
    public async Task UseAspNetCore_OptionsAction_FallbackFactory_CanResolveTenantFromRequest()
    {
        await using var app = await AspNetCoreIngressTestApp.CreateAsync(builder =>
        {
            builder.Services.AddContextR(ctx => ctx
                .Add<UserContext>(reg => reg
                    .Map(m => m.Property(c => c.TenantId, "X-Tenant-Id").Required())
                    .UseAspNetCore(o => o.Enforcement(e =>
                    {
                        e.Mode = ContextIngressEnforcementMode.FailRequest;
                        e.FallbackContextFactory = http =>
                        {
                            var tenant = http.Request.Query["tenant"].FirstOrDefault();
                            return tenant is null
                                ? null
                                : new UserContext { TenantId = tenant, UserId = "fallback-user" };
                        };
                    }))));
        });

        var json = await app.GetJsonAsync("/ingress/default?tenant=contoso");

        Assert.True(json.GetProperty("hasContext").GetBoolean());
        Assert.Equal("contoso", json.GetProperty("tenantId").GetString());
        Assert.Equal("fallback-user", json.GetProperty("userId").GetString());
    }

    [Fact]
    public async Task UseAspNetCore_OptionsAction_CustomFailureWriter_ReturnsCustomResponse()
    {
        await using var app = await AspNetCoreIngressTestApp.CreateAsync(builder =>
        {
            builder.Services.AddContextR(ctx => ctx
                .Add<UserContext>(reg => reg
                    .Map(m => m.Property(c => c.TenantId, "X-Tenant-Id").Required())
                    .UseAspNetCore(o => o.Enforcement(e =>
                    {
                        e.OnFailure = _ => ContextIngressFailureDecision.FailWithWriter(async http =>
                        {
                            http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            await http.Response.WriteAsync("tenant-context-required");
                        });
                    }))));
        });

        var (status, body) = await app.GetRawAsync("/ingress/default");

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Equal("tenant-context-required", body);
    }

    [Fact]
    public async Task UseAspNetCore_OptionsAction_FallbackFailure_UsesFailureReasonFallbackFailed()
    {
        var recorder = new FailureRecorder();
        await using var app = await AspNetCoreIngressTestApp.CreateAsync(builder =>
        {
            builder.Services.AddSingleton(recorder);
            builder.Services.AddContextR(ctx => ctx
                .Add<UserContext>(reg => reg
                    .Map(m => m.Property(c => c.TenantId, "X-Tenant-Id").Required())
                    .UseAspNetCore(o => o.Enforcement(e =>
                    {
                        e.Mode = ContextIngressEnforcementMode.ObserveOnly;
                        e.FallbackContextFactory = _ => throw new InvalidOperationException("fallback failed");
                        e.OnFailure = recorder.RecordAndContinue;
                    }))));
        });

        var json = await app.GetJsonAsync("/ingress/default");

        Assert.False(json.GetProperty("hasContext").GetBoolean());
        Assert.Equal(1, recorder.Invocations);
        Assert.Equal(ContextIngressFailureReason.FallbackFailed, recorder.LastReason);
    }

    [Fact]
    public async Task UseAspNetCore_OptionsFactoryFromServiceProvider_CanToggleStrictMode()
    {
        await using var app = await AspNetCoreIngressTestApp.CreateAsync(builder =>
        {
            builder.Services.AddSingleton(new FeatureFlags { StrictIngress = true });
            builder.Services.AddContextR(ctx => ctx
                .Add<UserContext>(reg => reg
                    .Map(m => m.Property(c => c.TenantId, "X-Tenant-Id").Required())
                    .UseAspNetCore(sp =>
                    {
                        var flags = sp.GetRequiredService<FeatureFlags>();
                        var options = new ContextRAspNetCoreOptions<UserContext>();
                        options.Enforcement(e =>
                        {
                            e.Mode = flags.StrictIngress
                                ? ContextIngressEnforcementMode.FailRequest
                                : ContextIngressEnforcementMode.ObserveOnly;
                        });
                        return options;
                    })));
        });

        var (status, _) = await app.GetRawAsync("/ingress/default");
        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task UseAspNetCore_ServiceProviderAndOptionsCallback_CanUseServicesForDecisions()
    {
        var recorder = new FailureRecorder();
        await using var app = await AspNetCoreIngressTestApp.CreateAsync(builder =>
        {
            builder.Services.AddSingleton(recorder);
            builder.Services.AddContextR(ctx => ctx
                .Add<UserContext>(reg => reg
                    .Map(m => m.Property(c => c.TenantId, "X-Tenant-Id").Required())
                    .UseAspNetCore((sp, o) =>
                    {
                        var r = sp.GetRequiredService<FailureRecorder>();
                        o.Enforcement(e =>
                        {
                            e.Mode = ContextIngressEnforcementMode.ObserveOnly;
                            e.OnFailure = r.RecordAndContinue;
                        });
                    })));
        });

        var json = await app.GetJsonAsync("/ingress/default");

        Assert.False(json.GetProperty("hasContext").GetBoolean());
        Assert.Equal(1, recorder.Invocations);
    }

    [Fact]
    public async Task UseAspNetCore_DomainConfiguration_UsesDomainAwareEnforcementContext()
    {
        var recorder = new FailureRecorder();
        await using var app = await AspNetCoreIngressTestApp.CreateAsync(builder =>
        {
            builder.Services.AddSingleton(recorder);
            builder.Services.AddContextR(ctx =>
            {
                ctx.Add<UserContext>();
                ctx.AddDomain("orders", d => d.Add<UserContext>(reg => reg
                    .Map(m => m.Property(c => c.TenantId, "X-Tenant-Id").Required())
                    .UseAspNetCore((sp, o) =>
                    {
                        var r = sp.GetRequiredService<FailureRecorder>();
                        o.Enforcement(e =>
                        {
                            e.Mode = ContextIngressEnforcementMode.ObserveOnly;
                            e.OnFailure = r.RecordAndContinue;
                        });
                    })));
            });
        });

        var json = await app.GetJsonAsync("/ingress/orders");

        Assert.False(json.GetProperty("hasContext").GetBoolean());
        Assert.Equal(1, recorder.Invocations);
        Assert.Equal("orders", recorder.LastDomain);
        Assert.Equal(ContextIngressFailureReason.ExtractionFailed, recorder.LastReason);
    }

    [Fact]
    public async Task UseAspNetCore_ConcurrentRequests_IsolatesContextsAcrossRequests()
    {
        await using var app = await AspNetCoreIngressTestApp.CreateAsync(builder =>
        {
            builder.Services.AddContextR(ctx => ctx
                .Add<UserContext>(reg => reg
                    .MapProperty(c => c.TenantId, "X-Tenant-Id")
                    .MapProperty(c => c.UserId, "X-User-Id")
                    .UseAspNetCore()));
        });

        var tasks = Enumerable.Range(1, 30).Select(async i =>
        {
            var tenant = $"tenant-{i}";
            var user = $"user-{i}";
            var json = await app.GetJsonAsync("/ingress/default",
                ("X-Tenant-Id", tenant),
                ("X-User-Id", user));

            return new
            {
                Tenant = json.GetProperty("tenantId").GetString(),
                User = json.GetProperty("userId").GetString(),
                ExpectedTenant = tenant,
                ExpectedUser = user
            };
        });

        var results = await Task.WhenAll(tasks);
        foreach (var r in results)
        {
            Assert.Equal(r.ExpectedTenant, r.Tenant);
            Assert.Equal(r.ExpectedUser, r.User);
        }
    }
}
