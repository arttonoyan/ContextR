using ContextR.Resolution;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.UnitTests;

public sealed class ResolutionOrchestratorTests
{
    [Fact]
    public void DefaultPolicy_ExternalBoundary_PrefersResolverOverPropagated()
    {
        using var provider = CreateProvider(services =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<UserContext>(reg => reg
                    .UseResolver<UserContext>(_ => new UserContext("resolved-external")));
            });
        });

        var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<UserContext>>();
        var result = orchestrator.Resolve(
            new ContextResolutionContext { Boundary = ContextIngressBoundary.External },
            propagatedContext: new UserContext("propagated-external"));

        Assert.NotNull(result.Context);
        Assert.Equal("resolved-external", result.Context!.UserId);
        Assert.Equal(ContextResolutionSource.Resolver, result.Source);
    }

    [Fact]
    public void DefaultPolicy_InternalBoundary_PrefersPropagatedOverResolver()
    {
        using var provider = CreateProvider(services =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<UserContext>(reg => reg
                    .UseResolver<UserContext>(_ => new UserContext("resolved-internal")));
            });
        });

        var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<UserContext>>();
        var result = orchestrator.Resolve(
            new ContextResolutionContext { Boundary = ContextIngressBoundary.Internal },
            propagatedContext: new UserContext("propagated-internal"));

        Assert.NotNull(result.Context);
        Assert.Equal("propagated-internal", result.Context!.UserId);
        Assert.Equal(ContextResolutionSource.Propagated, result.Source);
    }

    [Fact]
    public void ResolveAndWrite_WritesFinalValueToAmbientContext()
    {
        using var provider = CreateProvider(services =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<UserContext>(reg => reg
                    .UseResolver<UserContext>(_ => new UserContext("write-me")));
            });
        });

        var accessor = provider.GetRequiredService<IContextAccessor>();
        var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<UserContext>>();

        var result = orchestrator.ResolveAndWrite(new ContextResolutionContext
        {
            Boundary = ContextIngressBoundary.External
        });

        Assert.NotNull(result.Context);
        Assert.Equal("write-me", result.Context!.UserId);
        Assert.Equal("write-me", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void ResolverRegistry_UsesDomainSpecificResolver_WithDefaultFallback()
    {
        using var provider = CreateProvider(services =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<UserContext>(reg => reg
                    .UseResolver<UserContext>(_ => new UserContext("default-domain")));

                builder.AddDomain("web-api", domain =>
                {
                    domain.Add<UserContext>(reg => reg
                        .UseResolver<UserContext>(_ => new UserContext("web-domain")));
                });
            });
        });

        var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<UserContext>>();

        var web = orchestrator.Resolve(new ContextResolutionContext
        {
            Boundary = ContextIngressBoundary.External,
            Domain = "web-api"
        });

        var grpc = orchestrator.Resolve(new ContextResolutionContext
        {
            Boundary = ContextIngressBoundary.External,
            Domain = "grpc"
        });

        Assert.Equal("web-domain", web.Context?.UserId);
        Assert.Equal("default-domain", grpc.Context?.UserId);
    }

    [Fact]
    public void DomainSpecificPolicy_CanOverrideDefaultTrustBoundaryBehavior()
    {
        using var provider = CreateProvider(services =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<UserContext>(reg => reg
                    .UseResolver<UserContext>(_ => new UserContext("default-resolved")));

                builder.AddDomain("internal-high-trust", domain =>
                {
                    domain.Add<UserContext>(reg => reg
                        .UseResolver<UserContext>(_ => new UserContext("domain-resolved"))
                        .UseResolutionPolicy<UserContext>(policyContext =>
                            new ContextResolutionResult<UserContext>
                            {
                                Context = policyContext.ResolvedContext,
                                Source = ContextResolutionSource.Policy
                            }));
                });
            });
        });

        var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<UserContext>>();
        var result = orchestrator.Resolve(
            new ContextResolutionContext
            {
                Boundary = ContextIngressBoundary.Internal,
                Domain = "internal-high-trust"
            },
            propagatedContext: new UserContext("domain-propagated"));

        Assert.Equal("domain-resolved", result.Context?.UserId);
        Assert.Equal(ContextResolutionSource.Policy, result.Source);
    }

    private static ServiceProvider CreateProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.BuildServiceProvider();
    }

    private sealed record UserContext(string UserId);
}
