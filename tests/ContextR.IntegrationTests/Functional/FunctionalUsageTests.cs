using Microsoft.Extensions.DependencyInjection;

namespace ContextR.IntegrationTests.Functional;

public sealed class FunctionalUsageTests
{
    [Fact]
    public async Task MessageProcessing_PreservesAmbientTenant_WhileApplyingPerMessageUserSnapshot()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new TenantContext("tenant-root"));
        var messageSnapshots = new[]
        {
            accessor.CreateSnapshot(new UserContext("user-1")),
            accessor.CreateSnapshot(new UserContext("user-2")),
            accessor.CreateSnapshot(new UserContext("user-3"))
        };

        var processed = new List<(string UserId, string TenantId)>();
        foreach (var snapshot in messageSnapshots)
        {
            var item = await Task.Run(() =>
            {
                using (snapshot.BeginScope())
                {
                    var user = accessor.GetRequiredContext<UserContext>().UserId;
                    var tenant = accessor.GetRequiredContext<TenantContext>().TenantId;
                    return (user, tenant);
                }
            });

            processed.Add(item);
        }

        Assert.Equal(
            new[]
            {
                ("user-1", "tenant-root"),
                ("user-2", "tenant-root"),
                ("user-3", "tenant-root")
            },
            processed);
        Assert.Equal("tenant-root", accessor.GetContext<TenantContext>()?.TenantId);
        Assert.Null(accessor.GetContext<UserContext>());
    }

    [Fact]
    public void NestedOperationPipeline_RestoresBoundariesInOrder()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("root-user"));
        writer.SetContext(new TenantContext("root-tenant"));

        var outerSnapshot = accessor.CreateSnapshot(new UserContext("outer-user"));
        var innerSnapshot = accessor.CreateSnapshot(new UserContext("inner-user"));

        using (outerSnapshot.BeginScope())
        {
            Assert.Equal("outer-user", accessor.GetRequiredContext<UserContext>().UserId);
            Assert.Equal("root-tenant", accessor.GetRequiredContext<TenantContext>().TenantId);

            using (innerSnapshot.BeginScope())
            {
                Assert.Equal("inner-user", accessor.GetRequiredContext<UserContext>().UserId);
                Assert.Equal("root-tenant", accessor.GetRequiredContext<TenantContext>().TenantId);
            }

            Assert.Equal("outer-user", accessor.GetRequiredContext<UserContext>().UserId);
        }

        Assert.Equal("root-user", accessor.GetRequiredContext<UserContext>().UserId);
        Assert.Equal("root-tenant", accessor.GetRequiredContext<TenantContext>().TenantId);
    }

    [Fact]
    public async Task DomainContext_PropagatesAcrossAsyncFlows()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("root-user"));
        writer.SetContext("web-api", new UserContext("web-root"));
        writer.SetContext("grpc", new UserContext("grpc-root"));

        var snapshots = new[]
        {
            accessor.CreateSnapshot("web-api", new UserContext("web-msg-1")),
            accessor.CreateSnapshot("web-api", new UserContext("web-msg-2")),
        };

        var results = new List<(string WebUser, string GrpcUser, string DefaultUser)>();
        foreach (var snapshot in snapshots)
        {
            var item = await Task.Run(() =>
            {
                using (snapshot.BeginScope())
                {
                    return (
                        accessor.GetContext<UserContext>("web-api")!.UserId,
                        accessor.GetContext<UserContext>("grpc")!.UserId,
                        accessor.GetContext<UserContext>()!.UserId
                    );
                }
            });
            results.Add(item);
        }

        Assert.Equal("web-msg-1", results[0].WebUser);
        Assert.Equal("web-msg-2", results[1].WebUser);
        Assert.All(results, r => Assert.Equal("grpc-root", r.GrpcUser));
        Assert.All(results, r => Assert.Equal("root-user", r.DefaultUser));

        Assert.Equal("web-root", accessor.GetContext<UserContext>("web-api")?.UserId);
    }

    [Fact]
    public void NestedDomainOperations_RestoreBoundariesInOrder()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("root"));
        writer.SetContext("web-api", new UserContext("web-root"));

        var outerSnapshot = accessor.CreateSnapshot("web-api", new UserContext("web-outer"));
        var innerSnapshot = accessor.CreateSnapshot("web-api", new UserContext("web-inner"));

        using (outerSnapshot.BeginScope())
        {
            Assert.Equal("web-outer", accessor.GetContext<UserContext>("web-api")?.UserId);
            Assert.Equal("root", accessor.GetContext<UserContext>()?.UserId);

            using (innerSnapshot.BeginScope())
            {
                Assert.Equal("web-inner", accessor.GetContext<UserContext>("web-api")?.UserId);
                Assert.Equal("root", accessor.GetContext<UserContext>()?.UserId);
            }

            Assert.Equal("web-outer", accessor.GetContext<UserContext>("web-api")?.UserId);
        }

        Assert.Equal("web-root", accessor.GetContext<UserContext>("web-api")?.UserId);
        Assert.Equal("root", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void MultiTenantPipeline_WithDefaultDomainSelector()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.AddDomain("web-api", domain =>
            {
                domain.Add<UserContext>();
                domain.Add<TenantContext>();
            });
            builder.AddDomainPolicy(p => p.DefaultDomainSelector = _ => "web-api");
        });

        using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new TenantContext("acme-corp"));
        writer.SetContext(new UserContext("admin"));

        Assert.Equal("acme-corp", accessor.GetContext<TenantContext>()?.TenantId);
        Assert.Equal("acme-corp", accessor.GetContext<TenantContext>("web-api")?.TenantId);

        var snapshot = accessor.CreateSnapshot(new UserContext("operator"));

        using (snapshot.BeginScope())
        {
            Assert.Equal("operator", accessor.GetContext<UserContext>()?.UserId);
            Assert.Equal("operator", accessor.GetContext<UserContext>("web-api")?.UserId);
            Assert.Equal("acme-corp", accessor.GetContext<TenantContext>()?.TenantId);
        }

        Assert.Equal("admin", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public async Task ConcurrentAsyncFlows_IsolateAmbientContext()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new TenantContext("shared-tenant"));

        var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(() =>
        {
            var snapshot = accessor.CreateSnapshot(new UserContext($"user-{i}"));
            using (snapshot.BeginScope())
            {
                Thread.Sleep(10);
                var user = accessor.GetContext<UserContext>()?.UserId;
                var tenant = accessor.GetContext<TenantContext>()?.TenantId;
                return (user, tenant);
            }
        })).ToList();

        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal($"user-{i + 1}", results[i].user);
            Assert.Equal("shared-tenant", results[i].tenant);
        }
    }

    [Fact]
    public async Task ConcurrentAsyncFlows_WithDomains_AreIsolated()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", new TenantContext("shared-web-tenant"));
        writer.SetContext("grpc", new TenantContext("shared-grpc-tenant"));

        var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(() =>
        {
            var domain = i % 2 == 0 ? "web-api" : "grpc";
            var snapshot = accessor.CreateSnapshot(domain, new UserContext($"user-{i}"));
            using (snapshot.BeginScope())
            {
                Thread.Sleep(10);
                return (
                    User: accessor.GetContext<UserContext>(domain)?.UserId,
                    Domain: domain,
                    WebTenant: accessor.GetContext<TenantContext>("web-api")?.TenantId,
                    GrpcTenant: accessor.GetContext<TenantContext>("grpc")?.TenantId
                );
            }
        })).ToList();

        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal($"user-{i + 1}", results[i].User);
            Assert.Equal("shared-web-tenant", results[i].WebTenant);
            Assert.Equal("shared-grpc-tenant", results[i].GrpcTenant);
        }
    }

    [Fact]
    public void FullLifecycle_SetCaptureScopeRestore()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        Assert.Null(accessor.GetContext<UserContext>());
        Assert.Null(accessor.GetContext<TenantContext>());

        writer.SetContext(new UserContext("step1"));
        Assert.Equal("step1", accessor.GetContext<UserContext>()?.UserId);

        writer.SetContext(new TenantContext("tenant1"));
        var snapshot = accessor.CreateSnapshot();

        writer.SetContext(new UserContext("step2"));
        Assert.Equal("step2", accessor.GetContext<UserContext>()?.UserId);
        Assert.Equal("step1", snapshot.GetContext<UserContext>()?.UserId);

        using (snapshot.BeginScope())
        {
            Assert.Equal("step1", accessor.GetContext<UserContext>()?.UserId);
            Assert.Equal("tenant1", accessor.GetContext<TenantContext>()?.TenantId);

            writer.SetContext(new UserContext("step3"));
            Assert.Equal("step3", accessor.GetContext<UserContext>()?.UserId);
        }

        Assert.Equal("step2", accessor.GetContext<UserContext>()?.UserId);
        Assert.Equal("tenant1", accessor.GetContext<TenantContext>()?.TenantId);
    }

    [Fact]
    public void ScopedSnapshot_ViaServiceProvider_IsImmutableAtResolution()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("before-scope"));

        using var scope = provider.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IContextSnapshot>();

        writer.SetContext(new UserContext("after-scope"));

        Assert.Equal("before-scope", snapshot.GetContext<UserContext>()?.UserId);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<UserContext>();
            builder.Add<TenantContext>();
        });
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateDomainProvider()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<UserContext>();
            builder.Add<TenantContext>();
            builder.AddDomain("web-api", domain =>
            {
                domain.Add<UserContext>();
                domain.Add<TenantContext>();
            });
            builder.AddDomain("grpc", domain =>
            {
                domain.Add<UserContext>();
                domain.Add<TenantContext>();
            });
        });
        return services.BuildServiceProvider();
    }

    private sealed record UserContext(string UserId);
    private sealed record TenantContext(string TenantId);
}
