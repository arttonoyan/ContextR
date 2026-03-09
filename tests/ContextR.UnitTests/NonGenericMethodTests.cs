using ContextR.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.UnitTests;

public sealed class NonGenericMethodTests
{
    [Fact]
    public void Accessor_GetContext_ByType_ReturnsSetValue()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(typeof(UserContext), new UserContext("alice"));

        var result = accessor.GetContext(typeof(UserContext));

        Assert.IsType<UserContext>(result);
        Assert.Equal("alice", ((UserContext)result!).UserId);
    }

    [Fact]
    public void Accessor_GetContext_ByType_ReturnsNull_WhenNotSet()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        Assert.Null(accessor.GetContext(typeof(UserContext)));
    }

    [Fact]
    public void Accessor_GetContext_ByTypeAndDomain_ReturnsSetValue()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", typeof(UserContext), new UserContext("web-alice"));

        var result = accessor.GetContext("web-api", typeof(UserContext));

        Assert.IsType<UserContext>(result);
        Assert.Equal("web-alice", ((UserContext)result!).UserId);
    }

    [Fact]
    public void Accessor_GetContext_ByTypeAndDomain_ReturnsNull_WhenNotSet()
    {
        using var provider = CreateDomainProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        Assert.Null(accessor.GetContext("web-api", typeof(UserContext)));
    }

    [Fact]
    public void Accessor_GetContext_ByTypeAndDomain_IsIsolatedFromDefault()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(typeof(UserContext), new UserContext("default-user"));
        writer.SetContext("web-api", typeof(UserContext), new UserContext("web-user"));

        var defaultResult = (UserContext)accessor.GetContext(typeof(UserContext))!;
        var domainResult = (UserContext)accessor.GetContext("web-api", typeof(UserContext))!;

        Assert.Equal("default-user", defaultResult.UserId);
        Assert.Equal("web-user", domainResult.UserId);
    }

    [Fact]
    public void Writer_SetContext_ByType_NullClearsValue()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(typeof(UserContext), new UserContext("alice"));
        Assert.NotNull(accessor.GetContext(typeof(UserContext)));

        writer.SetContext(typeof(UserContext), null);
        Assert.Null(accessor.GetContext(typeof(UserContext)));
    }

    [Fact]
    public void Writer_SetContext_ByTypeAndDomain_NullClearsValue()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", typeof(UserContext), new UserContext("alice"));
        Assert.NotNull(accessor.GetContext("web-api", typeof(UserContext)));

        writer.SetContext("web-api", typeof(UserContext), null);
        Assert.Null(accessor.GetContext("web-api", typeof(UserContext)));
    }

    [Fact]
    public void Writer_SetContext_ByType_OverwritesExistingValue()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(typeof(UserContext), new UserContext("first"));
        writer.SetContext(typeof(UserContext), new UserContext("second"));

        Assert.Equal("second", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
    }

    [Fact]
    public void MultipleTypes_StoredAndRetrievedIndependently_ViaNonGeneric()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(typeof(UserContext), new UserContext("alice"));
        writer.SetContext(typeof(TenantContext), new TenantContext("acme"));

        Assert.Equal("alice", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
        Assert.Equal("acme", ((TenantContext)accessor.GetContext(typeof(TenantContext))!).TenantId);
    }

    [Fact]
    public void NonGeneric_And_GenericExtension_ReturnSameValues()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("alice"));

        var viaGeneric = accessor.GetContext<UserContext>();
        var viaNonGeneric = accessor.GetContext(typeof(UserContext));

        Assert.Same(viaGeneric, viaNonGeneric);
    }

    [Fact]
    public void NonGeneric_And_GenericExtension_ReturnSameValues_ForDomain()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", new UserContext("web-alice"));

        var viaGeneric = accessor.GetContext<UserContext>("web-api");
        var viaNonGeneric = accessor.GetContext("web-api", typeof(UserContext));

        Assert.Same(viaGeneric, viaNonGeneric);
    }

    [Fact]
    public void GenericSetContext_ReadViaNonGeneric_Interoperable()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("generic-write"));

        var result = accessor.GetContext(typeof(UserContext));
        Assert.Equal("generic-write", ((UserContext)result!).UserId);
    }

    [Fact]
    public void NonGenericSetContext_ReadViaGenericExtension_Interoperable()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(typeof(UserContext), new UserContext("non-generic-write"));

        var result = accessor.GetContext<UserContext>();
        Assert.Equal("non-generic-write", result?.UserId);
    }

    [Fact]
    public void CreateSnapshot_ByType_ContainsValue()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        var snapshot = accessor.CreateSnapshot(typeof(UserContext), new UserContext("snap-user"));

        var result = snapshot.GetContext(typeof(UserContext));
        Assert.IsType<UserContext>(result);
        Assert.Equal("snap-user", ((UserContext)result!).UserId);
    }

    [Fact]
    public void CreateSnapshot_ByType_ThrowsOnNull()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        Assert.Throws<ArgumentNullException>(() => accessor.CreateSnapshot(typeof(UserContext), null!));
    }

    [Fact]
    public void CreateSnapshot_ByTypeAndDomain_ContainsValueForDomain()
    {
        using var provider = CreateDomainProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        var snapshot = accessor.CreateSnapshot("web-api", typeof(UserContext), new UserContext("snap-web"));

        Assert.Equal("snap-web", ((UserContext)snapshot.GetContext("web-api", typeof(UserContext))!).UserId);
        Assert.Null(snapshot.GetContext(typeof(UserContext)));
    }

    [Fact]
    public void CreateSnapshot_ByTypeAndDomain_ThrowsOnNull()
    {
        using var provider = CreateDomainProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        Assert.Throws<ArgumentNullException>(
            () => accessor.CreateSnapshot("web-api", typeof(UserContext), null!));
    }

    [Fact]
    public void CreateSnapshot_Full_CapturesAllValues_ReadViaNonGeneric()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(typeof(UserContext), new UserContext("default-user"));
        writer.SetContext("web-api", typeof(UserContext), new UserContext("web-user"));
        writer.SetContext(typeof(TenantContext), new TenantContext("acme"));

        var snapshot = accessor.CreateSnapshot();

        writer.SetContext(typeof(UserContext), new UserContext("changed"));

        Assert.Equal("default-user", ((UserContext)snapshot.GetContext(typeof(UserContext))!).UserId);
        Assert.Equal("web-user", ((UserContext)snapshot.GetContext("web-api", typeof(UserContext))!).UserId);
        Assert.Equal("acme", ((TenantContext)snapshot.GetContext(typeof(TenantContext))!).TenantId);
    }

    [Fact]
    public void Snapshot_GetContext_ByType_ReturnsNull_WhenNotPresent()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        var snapshot = accessor.CreateSnapshot();

        Assert.Null(snapshot.GetContext(typeof(UserContext)));
    }

    [Fact]
    public void Snapshot_GetContext_ByTypeAndDomain_ReturnsNull_WhenNotPresent()
    {
        using var provider = CreateDomainProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        var snapshot = accessor.CreateSnapshot();

        Assert.Null(snapshot.GetContext("web-api", typeof(UserContext)));
    }

    [Fact]
    public void Snapshot_NonGeneric_And_GenericExtension_ReturnSameValues()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("alice"));
        var snapshot = accessor.CreateSnapshot();

        var viaGeneric = snapshot.GetContext<UserContext>();
        var viaNonGeneric = snapshot.GetContext(typeof(UserContext));

        Assert.Same(viaGeneric, viaNonGeneric);
    }

    [Fact]
    public void ContextSnapshot_NonGeneric_GetContext_ReturnsRawObject_WhenTypeMismatch()
    {
        var values = new Dictionary<ContextKey, object>
        {
            [new ContextKey(null, typeof(UserContext))] = "not-a-user-context"
        };

        var snapshot = new ContextSnapshot(values, null);

        var result = snapshot.GetContext(typeof(UserContext));
        Assert.IsType<string>(result);
        Assert.Equal("not-a-user-context", result);
    }

    [Fact]
    public void ContextSnapshot_NonGeneric_GetContextDomain_ReturnsRawObject_WhenTypeMismatch()
    {
        var values = new Dictionary<ContextKey, object>
        {
            [new ContextKey("domain", typeof(UserContext))] = "not-a-user-context"
        };

        var snapshot = new ContextSnapshot(values, null);

        var result = snapshot.GetContext("domain", typeof(UserContext));
        Assert.IsType<string>(result);
        Assert.Equal("not-a-user-context", result);
    }

    [Fact]
    public void BeginScope_WithNonGenericSnapshot_ActivatesAndRestores()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(typeof(UserContext), new UserContext("parent"));
        var snapshot = accessor.CreateSnapshot(typeof(UserContext), new UserContext("scoped"));

        using (snapshot.BeginScope())
        {
            Assert.Equal("scoped", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
        }

        Assert.Equal("parent", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
    }

    [Fact]
    public void BeginScope_WithNonGenericDomainSnapshot_ActivatesAndRestores()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", typeof(UserContext), new UserContext("parent"));
        var snapshot = accessor.CreateSnapshot("web-api", typeof(UserContext), new UserContext("scoped"));

        using (snapshot.BeginScope())
        {
            Assert.Equal("scoped", ((UserContext)accessor.GetContext("web-api", typeof(UserContext))!).UserId);
        }

        Assert.Equal("parent", ((UserContext)accessor.GetContext("web-api", typeof(UserContext))!).UserId);
    }

    [Fact]
    public void NestedScopes_WithNonGenericSnapshots_RestoreCorrectly()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(typeof(UserContext), new UserContext("root"));
        var outer = accessor.CreateSnapshot(typeof(UserContext), new UserContext("outer"));
        var inner = accessor.CreateSnapshot(typeof(UserContext), new UserContext("inner"));

        using (outer.BeginScope())
        {
            Assert.Equal("outer", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);

            using (inner.BeginScope())
            {
                Assert.Equal("inner", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
            }

            Assert.Equal("outer", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
        }

        Assert.Equal("root", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
    }

    [Fact]
    public void DefaultDomainSelector_WorksWithNonGenericMethods()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
            builder.AddDomainPolicy(p => p.DefaultDomainSelector = _ => "web-api");
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(typeof(UserContext), new UserContext("via-selector"));

        Assert.Equal("via-selector", ((UserContext)accessor.GetContext(typeof(UserContext))!).UserId);
        Assert.Equal("via-selector", ((UserContext)accessor.GetContext("web-api", typeof(UserContext))!).UserId);
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

    private static ServiceProvider CreateProvider(Action<IContextBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddContextR(configure);
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
