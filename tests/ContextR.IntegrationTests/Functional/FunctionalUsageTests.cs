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

    private sealed record UserContext(string UserId);
    private sealed record TenantContext(string TenantId);
}
