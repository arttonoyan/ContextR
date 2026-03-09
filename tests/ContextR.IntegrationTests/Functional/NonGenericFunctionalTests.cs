using Microsoft.Extensions.DependencyInjection;

namespace ContextR.IntegrationTests.Functional;

public sealed class NonGenericFunctionalTests
{
    [Fact]
    public void FullLifecycle_SetCaptureScopeRestore_ViaNonGeneric()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        Assert.Null(accessor.GetContext(typeof(UserContext)));
        Assert.Null(accessor.GetContext(typeof(TenantContext)));

        writer.SetContext(typeof(UserContext), new UserContext("step1"));
        Assert.Equal("step1", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);

        writer.SetContext(typeof(TenantContext), new TenantContext("tenant1"));
        var snapshot = accessor.CreateSnapshot();

        writer.SetContext(typeof(UserContext), new UserContext("step2"));
        Assert.Equal("step2", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
        Assert.Equal("step1", ((UserContext)snapshot.GetContext(typeof(UserContext))!).UserId);

        using (snapshot.BeginScope())
        {
            Assert.Equal("step1", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
            Assert.Equal("tenant1", ((TenantContext)accessor.GetContext(typeof(TenantContext))!).TenantId);

            writer.SetContext(typeof(UserContext), new UserContext("step3"));
            Assert.Equal("step3", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
        }

        Assert.Equal("step2", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
        Assert.Equal("tenant1", ((TenantContext)accessor.GetContext(typeof(TenantContext))!).TenantId);
    }

    [Fact]
    public async Task MessageProcessing_ViaNonGeneric_PreservesAmbientTenant()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(typeof(TenantContext), new TenantContext("tenant-root"));
        var messageSnapshots = new[]
        {
            accessor.CreateSnapshot(typeof(UserContext), new UserContext("user-1")),
            accessor.CreateSnapshot(typeof(UserContext), new UserContext("user-2")),
            accessor.CreateSnapshot(typeof(UserContext), new UserContext("user-3"))
        };

        var processed = new List<(string UserId, string TenantId)>();
        foreach (var snapshot in messageSnapshots)
        {
            var item = await Task.Run(() =>
            {
                using (snapshot.BeginScope())
                {
                    var user = ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId;
                    var tenant = ((TenantContext)accessor.GetContext(typeof(TenantContext))!).TenantId;
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
        Assert.Equal("tenant-root", ((TenantContext)accessor.GetContext(typeof(TenantContext))!).TenantId);
        Assert.Null(accessor.GetContext(typeof(UserContext)));
    }

    [Fact]
    public void NestedOperationPipeline_ViaNonGeneric_RestoresBoundariesInOrder()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(typeof(UserContext), new UserContext("root-user"));
        writer.SetContext(typeof(TenantContext), new TenantContext("root-tenant"));

        var outerSnapshot = accessor.CreateSnapshot(typeof(UserContext), new UserContext("outer-user"));
        var innerSnapshot = accessor.CreateSnapshot(typeof(UserContext), new UserContext("inner-user"));

        using (outerSnapshot.BeginScope())
        {
            Assert.Equal("outer-user", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
            Assert.Equal("root-tenant", ((TenantContext)accessor.GetContext(typeof(TenantContext))!).TenantId);

            using (innerSnapshot.BeginScope())
            {
                Assert.Equal("inner-user", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
                Assert.Equal("root-tenant", ((TenantContext)accessor.GetContext(typeof(TenantContext))!).TenantId);
            }

            Assert.Equal("outer-user", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
        }

        Assert.Equal("root-user", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
        Assert.Equal("root-tenant", ((TenantContext)accessor.GetContext(typeof(TenantContext))!).TenantId);
    }

    [Fact]
    public async Task DomainContext_ViaNonGeneric_PropagatesAcrossAsyncFlows()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(typeof(UserContext), new UserContext("root-user"));
        writer.SetContext("web-api", typeof(UserContext), new UserContext("web-root"));
        writer.SetContext("grpc", typeof(UserContext), new UserContext("grpc-root"));

        var snapshots = new[]
        {
            accessor.CreateSnapshot("web-api", typeof(UserContext), new UserContext("web-msg-1")),
            accessor.CreateSnapshot("web-api", typeof(UserContext), new UserContext("web-msg-2")),
        };

        var results = new List<(string WebUser, string GrpcUser, string DefaultUser)>();
        foreach (var snapshot in snapshots)
        {
            var item = await Task.Run(() =>
            {
                using (snapshot.BeginScope())
                {
                    return (
                        ((UserContext)accessor.GetContext("web-api", typeof(UserContext))!).UserId,
                        ((UserContext)accessor.GetContext("grpc", typeof(UserContext))!).UserId,
                        ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId
                    );
                }
            });
            results.Add(item);
        }

        Assert.Equal("web-msg-1", results[0].WebUser);
        Assert.Equal("web-msg-2", results[1].WebUser);
        Assert.All(results, r => Assert.Equal("grpc-root", r.GrpcUser));
        Assert.All(results, r => Assert.Equal("root-user", r.DefaultUser));

        Assert.Equal("web-root", ((UserContext)accessor.GetContext("web-api", typeof(UserContext))!).UserId);
    }

    [Fact]
    public async Task ConcurrentAsyncFlows_ViaNonGeneric_IsolateAmbientContext()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(typeof(TenantContext), new TenantContext("shared-tenant"));

        var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(() =>
        {
            var snapshot = accessor.CreateSnapshot(typeof(UserContext), new UserContext($"user-{i}"));
            using (snapshot.BeginScope())
            {
                Thread.Sleep(10);
                var user = ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId;
                var tenant = ((TenantContext)accessor.GetContext(typeof(TenantContext))!).TenantId;
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
    public async Task ConcurrentAsyncFlows_WithDomains_ViaNonGeneric_AreIsolated()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", typeof(TenantContext), new TenantContext("shared-web-tenant"));
        writer.SetContext("grpc", typeof(TenantContext), new TenantContext("shared-grpc-tenant"));

        var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(() =>
        {
            var domain = i % 2 == 0 ? "web-api" : "grpc";
            var snapshot = accessor.CreateSnapshot(domain, typeof(UserContext), new UserContext($"user-{i}"));
            using (snapshot.BeginScope())
            {
                Thread.Sleep(10);
                return (
                    User: ((UserContext)accessor.GetContext(domain, typeof(UserContext))!).UserId,
                    Domain: domain,
                    WebTenant: ((TenantContext)accessor.GetContext("web-api", typeof(TenantContext))!).TenantId,
                    GrpcTenant: ((TenantContext)accessor.GetContext("grpc", typeof(TenantContext))!).TenantId
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
    public void MixedGenericAndNonGeneric_SeesSameData()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("generic-default"));
        writer.SetContext("web-api", new TenantContext("generic-web-tenant"));

        Assert.Equal("generic-default", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
        Assert.Equal("generic-web-tenant", ((TenantContext)accessor.GetContext("web-api", typeof(TenantContext))!).TenantId);

        writer.SetContext(typeof(UserContext), new UserContext("non-generic-default"));
        writer.SetContext("web-api", typeof(TenantContext), new TenantContext("non-generic-web-tenant"));

        Assert.Equal("non-generic-default", accessor.GetContext<UserContext>()?.UserId);
        Assert.Equal("non-generic-web-tenant", accessor.GetContext<TenantContext>("web-api")?.TenantId);
    }

    [Fact]
    public void ScopedSnapshot_ViaServiceProvider_ReadableViaNonGeneric()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(typeof(UserContext), new UserContext("before-scope"));

        using var scope = provider.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IContextSnapshot>();

        writer.SetContext(typeof(UserContext), new UserContext("after-scope"));

        Assert.Equal("before-scope", ((UserContext)snapshot.GetContext(typeof(UserContext))!).UserId);
    }

    [Fact]
    public void ScopedSnapshot_WithDomains_ReadableViaNonGeneric()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(typeof(UserContext), new UserContext("default-user"));
        writer.SetContext("web-api", typeof(UserContext), new UserContext("web-user"));

        using var scope = provider.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IContextSnapshot>();

        Assert.Equal("default-user", ((UserContext)snapshot.GetContext(typeof(UserContext))!).UserId);
        Assert.Equal("web-user", ((UserContext)snapshot.GetContext("web-api", typeof(UserContext))!).UserId);
    }

    [Fact]
    public void NonGenericCreateSnapshot_IsImmutable_AfterAmbientChanges()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(typeof(UserContext), new UserContext("user-a"));
        var snapshot = accessor.CreateSnapshot();

        writer.SetContext(typeof(UserContext), new UserContext("user-b"));

        Assert.Equal("user-a", ((UserContext)snapshot.GetContext(typeof(UserContext))!).UserId);
        Assert.Equal("user-b", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
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
