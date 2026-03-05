using OpenFeature.Hosting;

namespace ContextR.OpenFeature.IntegrationTests;

public sealed class ContextROpenFeatureEndToEndIntegrationTests
{
    [Fact]
    public void ZeroConfig_ProducesEmptyEvaluationContext()
    {
        using var provider = CreateProvider(
            contextR: builder => builder.Add<UserContext>(),
            openFeature: feature => feature.UseContextR());

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IContextWriter>()
            .SetContext(new UserContext("u-zero", "zero@acme.dev", "secret-zero"));

        var context = scope.ServiceProvider.GetRequiredService<EvaluationContext>();
        Assert.Equal(0, context.Count);
        Assert.Null(context.TargetingKey);
    }

    [Fact]
    public void NonGenericUseContextR_ComposesMultipleContexts_AndSetsTargetingKeyAndKind()
    {
        using var provider = CreateProvider(
            contextR: builder =>
            {
                builder.Add<UserContext>();
                builder.Add<TenantContext>();
            },
            openFeature: feature =>
            {
                feature.UseContextR(options =>
                {
                    options.SetTargetingKey<UserContext>(x => x.UserId);
                    options.SetContextKind("user");
                    options.Map<UserContext>(map => map.MapProperty(x => x.Email, "user.email"));
                    options.Map<TenantContext>(map => map.MapProperty(x => x.TenantId, "tenant.id"));
                });
            });

        using var scope = provider.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IContextWriter>();
        writer.SetContext(new UserContext("u-1", "u1@acme.dev", "secret-1"));
        writer.SetContext(new TenantContext("t-1"));

        var context = scope.ServiceProvider.GetRequiredService<EvaluationContext>();
        Assert.Equal("u-1", context.TargetingKey);
        Assert.True(context.TryGetValue("kind", out var kind));
        Assert.Equal("user", kind?.AsString);
        Assert.True(context.TryGetValue("tenant.id", out var tenant));
        Assert.Equal("t-1", tenant?.AsString);
        Assert.True(context.TryGetValue("user.email", out var email));
        Assert.Equal("u1@acme.dev", email?.AsString);
    }

    [Fact]
    public void GenericUseContextR_SupportsPrimaryAndAdditionalMappings()
    {
        using var provider = CreateProvider(
            contextR: builder =>
            {
                builder.Add<UserContext>();
                builder.Add<TenantContext>();
            },
            openFeature: feature =>
            {
                feature.UseContextR<UserContext>(options =>
                {
                    options.SetTargetingKey(x => x.UserId);
                    options.SetContextKind("user");
                    options.Map(map => map.MapProperty(x => x.Email, "user.email"));
                    options.Map<TenantContext>(map => map.MapProperty(x => x.TenantId, "tenant.id"));
                });
            });

        using var scope = provider.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IContextWriter>();
        writer.SetContext(new UserContext("u-2", "u2@acme.dev", "secret-2"));
        writer.SetContext(new TenantContext("t-2"));

        var context = scope.ServiceProvider.GetRequiredService<EvaluationContext>();
        Assert.Equal("u-2", context.TargetingKey);
        Assert.True(context.TryGetValue("kind", out var kind));
        Assert.Equal("user", kind?.AsString);
        Assert.True(context.TryGetValue("user.email", out var email));
        Assert.Equal("u2@acme.dev", email?.AsString);
        Assert.True(context.TryGetValue("tenant.id", out var tenant));
        Assert.Equal("t-2", tenant?.AsString);
    }

    [Fact]
    public void DomainMapping_UsesSpecifiedDomainValues()
    {
        using var provider = CreateProvider(
            contextR: builder =>
            {
                builder.Add<UserContext>();
                builder.AddDomain("tenant-a", domain =>
                {
                    domain.Add<UserContext>();
                    domain.Add<TenantContext>();
                });
                builder.AddDomainPolicy(_ => "tenant-a");
            },
            openFeature: feature =>
            {
                feature.UseContextR(options =>
                {
                    options.SetTargetingKey<UserContext>(x => x.UserId, domain: "tenant-a");
                    options.Map<TenantContext>("tenant-a", map => map.MapProperty(x => x.TenantId, "tenant.id"));
                });
            });

        using var scope = provider.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IContextWriter>();
        writer.SetContext(new UserContext("user-default", "default@acme.dev", "secret-default"));
        writer.SetContext("tenant-a", new UserContext("user-domain", "domain@acme.dev", "secret-domain"));
        writer.SetContext("tenant-a", new TenantContext("tenant-domain"));

        var context = scope.ServiceProvider.GetRequiredService<EvaluationContext>();
        Assert.Equal("user-domain", context.TargetingKey);
        Assert.True(context.TryGetValue("tenant.id", out var tenant));
        Assert.Equal("tenant-domain", tenant?.AsString);
    }

    [Fact]
    public void MappingPolicies_ApplyAllowAndBlockRules()
    {
        using var provider = CreateProvider(
            contextR: builder => builder.Add<UserContext>(),
            openFeature: feature =>
            {
                feature.UseContextR<UserContext>(options =>
                {
                    options.AllowKeys("user.email", "user.secret");
                    options.BlockKeys("user.secret");
                    options.Map(map => map
                        .MapProperty(x => x.Email, "user.email")
                        .MapProperty(x => x.Secret, "user.secret"));
                });
            });

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IContextWriter>()
            .SetContext(new UserContext("u-4", "u4@acme.dev", "secret-4"));

        var context = scope.ServiceProvider.GetRequiredService<EvaluationContext>();
        Assert.True(context.TryGetValue("user.email", out var email));
        Assert.Equal("u4@acme.dev", email?.AsString);
        Assert.False(context.ContainsKey("user.secret"));
    }

    [Fact]
    public void CollisionBehaviorThrow_FailsOnDuplicateKeys()
    {
        using var provider = CreateProvider(
            contextR: builder =>
            {
                builder.Add<UserContext>();
                builder.Add<TenantContext>();
            },
            openFeature: feature =>
            {
                feature.UseContextR(options =>
                {
                    options.CollisionBehavior = ContextROpenFeatureCollisionBehavior.Throw;
                    options.Map<UserContext>(map => map.MapProperty(x => x.UserId, "dup.key"));
                    options.Map<TenantContext>(map => map.MapProperty(x => x.TenantId, "dup.key"));
                });
            });

        using var scope = provider.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IContextWriter>();
        writer.SetContext(new UserContext("u-dup", "dup@acme.dev", "secret-dup"));
        writer.SetContext(new TenantContext("t-dup"));

        Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<EvaluationContext>());
    }

    [Fact]
    public void DomainScopedOpenFeatureClients_AreResolvable()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder => builder.Add<UserContext>());
        services.AddOpenFeature(feature =>
        {
            feature.UseContextR(options => options.SetContextKind("default"));
            feature.AddProvider(_ => new NoOpProvider("default"));
            feature.AddProvider("beta", (_, _) => new NoOpProvider("beta"));
            feature.AddPolicyName(policy => policy.DefaultNameSelector = _ => "beta");
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFeatureClient>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredKeyedService<IFeatureClient>("beta"));
    }

    [Fact]
    public async Task AsyncScopes_IsolateEvaluationContextPerFlow()
    {
        using var provider = CreateProvider(
            contextR: builder => builder.Add<UserContext>(),
            openFeature: feature =>
            {
                feature.UseContextR<UserContext>(options => options.SetTargetingKey(x => x.UserId));
            });

        using var scope = provider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IContextAccessor>();
        var tasks = Enumerable.Range(1, 8).Select(i => Task.Run(() =>
        {
            var snapshot = accessor.CreateSnapshot(new UserContext($"user-{i}", $"u{i}@acme.dev", $"secret-{i}"));
            using (snapshot.BeginScope())
            {
                return scope.ServiceProvider.GetRequiredService<EvaluationContext>().TargetingKey;
            }
        }));

        var keys = await Task.WhenAll(tasks);
        for (var i = 1; i <= 8; i++)
        {
            Assert.Contains($"user-{i}", keys);
        }
    }

    private static ServiceProvider CreateProvider(
        Action<IContextBuilder> contextR,
        Action<OpenFeatureBuilder> openFeature)
    {
        var services = new ServiceCollection();
        services.AddContextR(contextR);
        services.AddOpenFeature(openFeature);
        return services.BuildServiceProvider();
    }

    private sealed record UserContext(string UserId, string Email, string Secret);
    private sealed record TenantContext(string TenantId);

    private sealed class NoOpProvider(string name) : FeatureProvider
    {
        public override Metadata? GetMetadata() => new(name);
        public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResolutionDetails<bool>(flagKey, defaultValue));
        public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResolutionDetails<string>(flagKey, defaultValue));
        public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResolutionDetails<int>(flagKey, defaultValue));
        public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResolutionDetails<double>(flagKey, defaultValue));
        public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResolutionDetails<Value>(flagKey, defaultValue));
    }
}
