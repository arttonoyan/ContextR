namespace ContextR.OpenFeature.UnitTests;

public sealed class ContextROpenFeatureIntegrationTests
{
    [Fact]
    public void UseContextR_IsChainable()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<UserContext>());

        services.AddOpenFeature(featureBuilder =>
        {
            var result = featureBuilder.UseContextR();
            Assert.Same(featureBuilder, result);
        });
    }

    [Fact]
    public void UseContextR_MapsTargetingKeyKindAndProperties()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx =>
        {
            ctx.Add<UserContext>();
            ctx.Add<TenantContext>();
        });

        services.AddOpenFeature(featureBuilder =>
        {
            featureBuilder.UseContextR(options =>
            {
                options.SetTargetingKey<UserContext>(x => x.UserId);
                options.SetContextKind("user");
                options.Map<UserContext>(map => map.MapProperty(x => x.Email, "user.email"));
                options.Map<TenantContext>(map => map.MapProperty(x => x.TenantId, "tenant.id"));
            });
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var writer = scope.ServiceProvider.GetRequiredService<IContextWriter>();
        writer.SetContext(new UserContext("u-1", "user@acme.dev", "secret"));
        writer.SetContext(new TenantContext("t-1"));

        var context = scope.ServiceProvider.GetRequiredService<EvaluationContext>();
        Assert.Equal("u-1", context.TargetingKey);
        Assert.True(context.TryGetValue("kind", out var kind));
        Assert.Equal("user", kind?.AsString);
        Assert.True(context.TryGetValue("user.email", out var email));
        Assert.Equal("user@acme.dev", email?.AsString);
        Assert.True(context.TryGetValue("tenant.id", out var tenant));
        Assert.Equal("t-1", tenant?.AsString);
    }

    [Fact]
    public void GenericUseContextR_SupportsConventionAndIgnore()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<UserContext>());

        services.AddOpenFeature(featureBuilder =>
        {
            featureBuilder.UseContextR<UserContext>(options =>
            {
                options.SetTargetingKey(x => x.UserId);
                options.Map(map => map.ByConvention("user.").Ignore(x => x.Secret));
            });
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var writer = scope.ServiceProvider.GetRequiredService<IContextWriter>();
        writer.SetContext(new UserContext("u-2", "u2@acme.dev", "secret-2"));

        var context = scope.ServiceProvider.GetRequiredService<EvaluationContext>();
        Assert.Equal("u-2", context.TargetingKey);
        Assert.True(context.TryGetValue("user.Email", out var email));
        Assert.Equal("u2@acme.dev", email?.AsString);
        Assert.False(context.TryGetValue("user.Secret", out _));
    }

    [Fact]
    public void UseContextR_ThrowsOnCollision_WhenConfigured()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx =>
        {
            ctx.Add<UserContext>();
            ctx.Add<TenantContext>();
        });

        services.AddOpenFeature(featureBuilder =>
        {
            featureBuilder.UseContextR(options =>
            {
                options.CollisionBehavior = ContextROpenFeatureCollisionBehavior.Throw;
                options.Map<UserContext>(map => map.MapProperty(x => x.UserId, "dup.key"));
                options.Map<TenantContext>(map => map.MapProperty(x => x.TenantId, "dup.key"));
            });
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IContextWriter>();
        writer.SetContext(new UserContext("u-1", "u1@acme.dev", "secret"));
        writer.SetContext(new TenantContext("t-1"));

        Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<EvaluationContext>());
    }

    [Fact]
    public void UseContextR_CanReadFromConfiguredDomain()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx =>
        {
            ctx.AddDomain("tenant-a", domain => domain.Add<UserContext>());
            ctx.AddDomainPolicy(_ => "tenant-a");
        });

        services.AddOpenFeature(featureBuilder =>
        {
            featureBuilder.UseContextR(options =>
            {
                options.Map<UserContext>("tenant-a", map => map.MapProperty(x => x.UserId, "user.id"));
            });
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IContextWriter>();
        writer.SetContext("tenant-a", new UserContext("domain-u", "d@acme.dev", "s"));

        var context = scope.ServiceProvider.GetRequiredService<EvaluationContext>();
        Assert.True(context.TryGetValue("user.id", out var userId));
        Assert.Equal("domain-u", userId?.AsString);
    }

    private sealed record UserContext(string UserId, string Email, string Secret);

    private sealed record TenantContext(string TenantId);
}
