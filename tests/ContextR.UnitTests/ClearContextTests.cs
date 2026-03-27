using Microsoft.Extensions.DependencyInjection;

namespace ContextR.UnitTests;

public sealed class ClearContextTests
{
    [Fact]
    public void ClearContext_RemovesDefaultValue()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("alice"));
        Assert.Equal("alice", accessor.GetContext<UserContext>()?.UserId);

        writer.ClearContext<UserContext>();
        Assert.Null(accessor.GetContext<UserContext>());
    }

    [Fact]
    public void ClearContext_RemovesDomainValue()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", new UserContext("alice"));
        Assert.Equal("alice", accessor.GetContext<UserContext>("web-api")?.UserId);

        writer.ClearContext<UserContext>("web-api");
        Assert.Null(accessor.GetContext<UserContext>("web-api"));
    }

    [Fact]
    public void ClearContext_DoesNotAffectOtherContextTypes()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("alice"));
        writer.SetContext(new TenantContext("acme"));

        writer.ClearContext<UserContext>();

        Assert.Null(accessor.GetContext<UserContext>());
        Assert.Equal("acme", accessor.GetContext<TenantContext>()?.TenantId);
    }

    [Fact]
    public void ClearContext_DoesNotAffectOtherDomains()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("default-user"));
        writer.SetContext("web-api", new UserContext("web-user"));

        writer.ClearContext<UserContext>("web-api");

        Assert.Null(accessor.GetContext<UserContext>("web-api"));
        Assert.Equal("default-user", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void ClearContext_DoesNotAffectDefaultWhenClearingDomain()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("default-user"));
        writer.SetContext("web-api", new UserContext("web-user"));

        writer.ClearContext<UserContext>();

        Assert.Null(accessor.GetContext<UserContext>());
        Assert.Equal("web-user", accessor.GetContext<UserContext>("web-api")?.UserId);
    }

    [Fact]
    public void ClearContext_IsIdempotent_WhenAlreadyNull()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        Assert.Null(accessor.GetContext<UserContext>());

        writer.ClearContext<UserContext>();
        Assert.Null(accessor.GetContext<UserContext>());
    }

    [Fact]
    public void ClearContext_NonGeneric_RemovesDefaultValue()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(typeof(UserContext), new UserContext("alice"));
        Assert.NotNull(accessor.GetContext(typeof(UserContext)));

        writer.ClearContext(typeof(UserContext));
        Assert.Null(accessor.GetContext(typeof(UserContext)));
    }

    [Fact]
    public void ClearContext_NonGeneric_RemovesDomainValue()
    {
        using var provider = CreateDomainProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", typeof(UserContext), new UserContext("alice"));
        Assert.NotNull(accessor.GetContext("web-api", typeof(UserContext)));

        writer.ClearContext("web-api", typeof(UserContext));
        Assert.Null(accessor.GetContext("web-api", typeof(UserContext)));
    }

    [Fact]
    public void ClearContext_ValueCanBeResetAfterClearing()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("first"));
        writer.ClearContext<UserContext>();
        writer.SetContext(new UserContext("second"));

        Assert.Equal("second", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void ClearContext_WithDefaultDomainSelector_ClearsResolvedDomain()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
            builder.AddDomainPolicy(p => p.DefaultDomainSelector = _ => "web-api");
        });

        using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("alice"));
        Assert.Equal("alice", accessor.GetContext<UserContext>()?.UserId);
        Assert.Equal("alice", accessor.GetContext<UserContext>("web-api")?.UserId);

        writer.ClearContext<UserContext>();

        Assert.Null(accessor.GetContext<UserContext>());
        Assert.Null(accessor.GetContext<UserContext>("web-api"));
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
