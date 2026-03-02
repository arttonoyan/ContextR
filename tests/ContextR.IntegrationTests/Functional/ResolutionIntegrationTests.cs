using ContextR.Resolution;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.IntegrationTests.Functional;

public sealed class ResolutionIntegrationTests
{
    [Fact]
    public void ExternalIngress_ResolverWins_AndWritesAmbientContext()
    {
        using var provider = CreateProvider(services =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<UserContext>(reg => reg
                    .UseResolver(_ => new UserContext("resolved-user")));
            });
        });

        var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<UserContext>>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        var result = orchestrator.ResolveAndWrite(
            new ContextResolutionContext
            {
                Boundary = ContextIngressBoundary.External,
                Source = "gateway-jwt"
            },
            propagatedContext: new UserContext("propagated-user"));

        Assert.Equal("resolved-user", result.Context?.UserId);
        Assert.Equal(ContextResolutionSource.Resolver, result.Source);
        Assert.Equal("resolved-user", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void InternalIngress_PropagatedWins_ByDefault()
    {
        using var provider = CreateProvider(services =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<UserContext>(reg => reg
                    .UseResolver(_ => new UserContext("resolved-internal")));
            });
        });

        var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<UserContext>>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        var result = orchestrator.ResolveAndWrite(
            new ContextResolutionContext
            {
                Boundary = ContextIngressBoundary.Internal,
                Source = "mesh-http"
            },
            propagatedContext: new UserContext("propagated-internal"));

        Assert.Equal("propagated-internal", result.Context?.UserId);
        Assert.Equal(ContextResolutionSource.Propagated, result.Source);
        Assert.Equal("propagated-internal", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void DomainSpecificPolicy_CanOverrideDefaultPrecedence()
    {
        using var provider = CreateProvider(services =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<UserContext>(reg => reg
                    .UseResolver(_ => new UserContext("default-resolved")));

                builder.AddDomain("internal-priority-resolver", d =>
                {
                    d.Add<UserContext>(reg => reg
                        .UseResolver(_ => new UserContext("domain-resolved"))
                        .UseResolutionPolicy(ctx =>
                            new ContextResolutionResult<UserContext>
                            {
                                Context = ctx.ResolvedContext,
                                Source = ContextResolutionSource.Policy
                            }));
                });
            });
        });

        var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<UserContext>>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        var result = orchestrator.ResolveAndWrite(
            new ContextResolutionContext
            {
                Boundary = ContextIngressBoundary.Internal,
                Domain = "internal-priority-resolver"
            },
            propagatedContext: new UserContext("domain-propagated"));

        Assert.Equal("domain-resolved", result.Context?.UserId);
        Assert.Equal(ContextResolutionSource.Policy, result.Source);
        Assert.Equal("domain-resolved", accessor.GetContext<UserContext>("internal-priority-resolver")?.UserId);
    }

    [Fact]
    public void DomainResolver_FallsBackToDefaultWhenSpecificDomainIsMissing()
    {
        using var provider = CreateProvider(services =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<UserContext>(reg => reg
                    .UseResolver(_ => new UserContext("default-domain")));

                builder.AddDomain("web-api", d =>
                {
                    d.Add<UserContext>(reg => reg
                        .UseResolver(_ => new UserContext("web-domain")));
                });
            });
        });

        var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<UserContext>>();

        var grpcResult = orchestrator.Resolve(new ContextResolutionContext
        {
            Boundary = ContextIngressBoundary.External,
            Domain = "grpc"
        });

        var webResult = orchestrator.Resolve(new ContextResolutionContext
        {
            Boundary = ContextIngressBoundary.External,
            Domain = "web-api"
        });

        Assert.Equal("default-domain", grpcResult.Context?.UserId);
        Assert.Equal("web-domain", webResult.Context?.UserId);
    }

    [Fact]
    public void ResolveAndWrite_WithNoSources_DoesNotWriteAmbientContext()
    {
        using var provider = CreateProvider(services =>
        {
            services.AddContextRResolution();
            services.AddContextR(builder => builder.Add<UserContext>());
        });

        var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<UserContext>>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        var result = orchestrator.ResolveAndWrite(new ContextResolutionContext
        {
            Boundary = ContextIngressBoundary.External
        });

        Assert.Null(result.Context);
        Assert.Equal(ContextResolutionSource.None, result.Source);
        Assert.Null(accessor.GetContext<UserContext>());
    }

    [Fact]
    public async Task ConcurrentIngressOperations_AreIsolatedAcrossAsyncFlows()
    {
        using var provider = CreateProvider(services =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<UserContext>(reg => reg
                    .UseResolver(ctx => new UserContext($"resolved-{ctx.Source}")));
            });
        });

        var accessor = provider.GetRequiredService<IContextAccessor>();

        var tasks = Enumerable.Range(1, 20).Select(i => Task.Run(() =>
        {
            var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<UserContext>>();
            var result = orchestrator.ResolveAndWrite(
                new ContextResolutionContext
                {
                    Boundary = ContextIngressBoundary.External,
                    Source = i.ToString()
                },
                propagatedContext: new UserContext($"propagated-{i}"));

            var ambient = accessor.GetContext<UserContext>()?.UserId;
            return (Result: result.Context?.UserId, Ambient: ambient);
        })).ToArray();

        var results = await Task.WhenAll(tasks);
        for (var i = 1; i <= 20; i++)
        {
            var expected = $"resolved-{i}";
            Assert.Equal(expected, results[i - 1].Result);
            Assert.Equal(expected, results[i - 1].Ambient);
        }
    }

    private static ServiceProvider CreateProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.BuildServiceProvider();
    }

    private sealed record UserContext(string UserId);
}
