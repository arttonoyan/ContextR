using Microsoft.Extensions.DependencyInjection;

namespace ContextR.UnitTests;

public sealed class BeginScopeExtensionTests
{
    [Fact]
    public void BeginScope_ActivatesValue_AndRestoresOnDispose()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("parent"));

        using (accessor.BeginScope(new UserContext("scoped")))
        {
            Assert.Equal("scoped", accessor.GetContext<UserContext>()?.UserId);
        }

        Assert.Equal("parent", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void BeginScope_Domain_ActivatesValue_AndRestoresOnDispose()
    {
        using var provider = CreateDomainProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext("web-api", new UserContext("parent"));

        using (accessor.BeginScope("web-api", new UserContext("scoped")))
        {
            Assert.Equal("scoped", accessor.GetContext<UserContext>("web-api")?.UserId);
        }

        Assert.Equal("parent", accessor.GetContext<UserContext>("web-api")?.UserId);
    }

    [Fact]
    public void BeginScope_NestedScopes_RestoreCorrectly()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("root"));

        using (accessor.BeginScope(new UserContext("outer")))
        {
            Assert.Equal("outer", accessor.GetContext<UserContext>()?.UserId);

            using (accessor.BeginScope(new UserContext("inner")))
            {
                Assert.Equal("inner", accessor.GetContext<UserContext>()?.UserId);
            }

            Assert.Equal("outer", accessor.GetContext<UserContext>()?.UserId);
        }

        Assert.Equal("root", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void BeginScope_DoesNotAffectOtherContextTypes()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("user-parent"));
        writer.SetContext(new TenantContext("tenant-parent"));

        using (accessor.BeginScope(new UserContext("user-scoped")))
        {
            Assert.Equal("user-scoped", accessor.GetContext<UserContext>()?.UserId);
            Assert.Equal("tenant-parent", accessor.GetContext<TenantContext>()?.TenantId);
        }

        Assert.Equal("user-parent", accessor.GetContext<UserContext>()?.UserId);
        Assert.Equal("tenant-parent", accessor.GetContext<TenantContext>()?.TenantId);
    }

    [Fact]
    public void BeginScope_Domain_DoesNotAffectDefaultSlot()
    {
        using var provider = CreateDomainProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("default-user"));
        writer.SetContext("web-api", new UserContext("web-user"));

        using (accessor.BeginScope("web-api", new UserContext("web-scoped")))
        {
            Assert.Equal("web-scoped", accessor.GetContext<UserContext>("web-api")?.UserId);
            Assert.Equal("default-user", accessor.GetContext<UserContext>()?.UserId);
        }

        Assert.Equal("web-user", accessor.GetContext<UserContext>("web-api")?.UserId);
        Assert.Equal("default-user", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public async Task BeginScope_InTask_DoesNotClearParentContext()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("parent"));

        var childResult = await Task.Run(() =>
        {
            using (accessor.BeginScope(new UserContext("child")))
            {
                return accessor.GetContext<UserContext>()?.UserId;
            }
        });

        Assert.Equal("child", childResult);
        Assert.Equal("parent", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public async Task BeginScope_Domain_InTask_DoesNotClearParentContext()
    {
        using var provider = CreateDomainProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext("web-api", new UserContext("parent"));

        var childResult = await Task.Run(() =>
        {
            using (accessor.BeginScope("web-api", new UserContext("child")))
            {
                return accessor.GetContext<UserContext>("web-api")?.UserId;
            }
        });

        Assert.Equal("child", childResult);
        Assert.Equal("parent", accessor.GetContext<UserContext>("web-api")?.UserId);
    }

    [Fact]
    public void BeginScope_RestoresToNull_WhenNoPreviousValue()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        Assert.Null(accessor.GetContext<UserContext>());

        using (accessor.BeginScope(new UserContext("scoped")))
        {
            Assert.Equal("scoped", accessor.GetContext<UserContext>()?.UserId);
        }

        Assert.Null(accessor.GetContext<UserContext>());
    }

    [Fact]
    public void BeginScope_Throws_WhenContextIsNull()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        Assert.Throws<ArgumentNullException>(() => accessor.BeginScope<UserContext>(null!));
    }

    [Fact]
    public void BeginScope_Domain_Throws_WhenContextIsNull()
    {
        using var provider = CreateDomainProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        Assert.Throws<ArgumentNullException>(() => accessor.BeginScope<UserContext>("web-api", null!));
    }

    [Fact]
    public void BeginScope_DoubleDispose_IsIdempotent()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("parent"));
        var scope = accessor.BeginScope(new UserContext("scoped"));

        Assert.Equal("scoped", accessor.GetContext<UserContext>()?.UserId);

        scope.Dispose();
        Assert.Equal("parent", accessor.GetContext<UserContext>()?.UserId);

        scope.Dispose();
        Assert.Equal("parent", accessor.GetContext<UserContext>()?.UserId);
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
        });
        return services.BuildServiceProvider();
    }

    private sealed record UserContext(string UserId);
    private sealed record TenantContext(string TenantId);
}
