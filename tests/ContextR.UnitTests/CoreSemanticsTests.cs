using Microsoft.Extensions.DependencyInjection;

namespace ContextR.UnitTests;

public sealed class CoreSemanticsTests
{
    [Fact]
    public void CreateSnapshot_IsImmutable_AfterAmbientChanges()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("user-a"));
        var snapshot = accessor.CreateSnapshot();

        writer.SetContext(new UserContext("user-b"));

        Assert.Equal("user-a", snapshot.GetContext<UserContext>()?.UserId);
        Assert.Equal("user-b", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void BeginScope_ActivatesSnapshot_AndRestoresPreviousState()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("user-parent"));
        var snapshot = accessor.CreateSnapshot(new UserContext("user-scope"));

        using (snapshot.BeginScope())
        {
            Assert.Equal("user-scope", accessor.GetContext<UserContext>()?.UserId);
        }

        Assert.Equal("user-parent", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void BeginScope_NestedScopes_RestoreCorrectValues()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("user-parent"));
        var outer = accessor.CreateSnapshot(new UserContext("user-outer"));
        var inner = accessor.CreateSnapshot(new UserContext("user-inner"));

        using (outer.BeginScope())
        {
            Assert.Equal("user-outer", accessor.GetContext<UserContext>()?.UserId);

            using (inner.BeginScope())
            {
                Assert.Equal("user-inner", accessor.GetContext<UserContext>()?.UserId);
            }

            Assert.Equal("user-outer", accessor.GetContext<UserContext>()?.UserId);
        }

        Assert.Equal("user-parent", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public async Task BeginScope_InTask_DoesNotClearParentContext()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("user-parent"));
        var childSnapshot = accessor.CreateSnapshot(new UserContext("user-child"));

        var childResult = await Task.Run(() =>
        {
            using (childSnapshot.BeginScope())
            {
                return accessor.GetContext<UserContext>()?.UserId;
            }
        });

        Assert.Equal("user-child", childResult);
        Assert.Equal("user-parent", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public async Task BeginScope_UsesRawRestore_AndPreservesFlowInBothParentAndChild()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("user-parent"));
        var scopedSnapshot = accessor.CreateSnapshot(new UserContext("user-scoped"));

        var afterScopeInChild = await Task.Run(() =>
        {
            using (scopedSnapshot.BeginScope())
            {
                Assert.Equal("user-scoped", accessor.GetContext<UserContext>()?.UserId);
            }

            return accessor.GetContext<UserContext>()?.UserId;
        });

        Assert.Equal("user-parent", afterScopeInChild);
        Assert.Equal("user-parent", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void MultipleContextTypes_AreStoredAndRetrievedIndependently()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("user-a"));
        writer.SetContext(new TenantContext("tenant-a"));

        var snapshot = accessor.CreateSnapshot();
        Assert.Equal("user-a", snapshot.GetContext<UserContext>()?.UserId);
        Assert.Equal("tenant-a", snapshot.GetContext<TenantContext>()?.TenantId);

        writer.SetContext<UserContext>(null);

        Assert.Null(accessor.GetContext<UserContext>());
        Assert.Equal("tenant-a", accessor.GetContext<TenantContext>()?.TenantId);
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
