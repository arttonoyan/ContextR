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

    [Fact]
    public void UseContextR_LastWriteWinsByDefault_ForDuplicateKeys()
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
                options.Map<UserContext>(map => map.MapProperty(x => x.UserId, "dup.key"));
                options.Map<TenantContext>(map => map.MapProperty(x => x.TenantId, "dup.key"));
            });
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IContextWriter>();
        writer.SetContext(new UserContext("u-1", "u1@acme.dev", "s"));
        writer.SetContext(new TenantContext("t-1"));

        var context = scope.ServiceProvider.GetRequiredService<EvaluationContext>();
        Assert.True(context.TryGetValue("dup.key", out var value));
        Assert.Equal("t-1", value?.AsString);
    }

    [Fact]
    public void UseContextR_IncludeNullValues_WritesNullAttributes()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<NullableContext>());

        services.AddOpenFeature(featureBuilder =>
        {
            featureBuilder.UseContextR(options =>
            {
                options.IncludeNullValues = true;
                options.Map<NullableContext>(map => map.MapProperty(x => x.OptionalValue, "opt.value"));
            });
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IContextWriter>()
            .SetContext(new NullableContext(null));

        var context = scope.ServiceProvider.GetRequiredService<EvaluationContext>();
        Assert.True(context.TryGetValue("opt.value", out var value));
        Assert.True(value?.IsNull);
    }

    [Fact]
    public void UseContextR_UnsupportedValue_IsIgnoredByDefault()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<UnsupportedContext>());

        services.AddOpenFeature(featureBuilder =>
        {
            featureBuilder.UseContextR(options =>
            {
                options.Map<UnsupportedContext>(map => map.MapProperty(x => x.Payload, "payload"));
            });
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IContextWriter>()
            .SetContext(new UnsupportedContext(new UnconvertiblePayload("abc")));

        var context = scope.ServiceProvider.GetRequiredService<EvaluationContext>();
        Assert.False(context.ContainsKey("payload"));
    }

    [Fact]
    public void UseContextR_UnsupportedValue_ThrowsWhenConfigured()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<UnsupportedContext>());

        services.AddOpenFeature(featureBuilder =>
        {
            featureBuilder.UseContextR(options =>
            {
                options.UnsupportedValueBehavior = ContextROpenFeatureUnsupportedValueBehavior.Throw;
                options.Map<UnsupportedContext>(map => map.MapProperty(x => x.Payload, "payload"));
            });
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IContextWriter>()
            .SetContext(new UnsupportedContext(new UnconvertiblePayload("abc")));

        Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<EvaluationContext>());
    }

    [Fact]
    public void UseContextR_ConvertsPrimitiveAndStructuredValues()
    {
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<RichContext>());

        services.AddOpenFeature(featureBuilder =>
        {
            featureBuilder.UseContextR(options =>
            {
                options.Map<RichContext>(map => map
                    .MapProperty(x => x.IsEnabled, "is.enabled")
                    .MapProperty(x => x.ShortCode, "short.code")
                    .MapProperty(x => x.ByteCode, "byte.code")
                    .MapProperty(x => x.LongId, "long.id")
                    .MapProperty(x => x.FloatValue, "float.value")
                    .MapProperty(x => x.DoubleValue, "double.value")
                    .MapProperty(x => x.DecimalValue, "decimal.value")
                    .MapProperty(x => x.Timestamp, "timestamp")
                    .MapProperty(x => x.CorrelationId, "correlation.id")
                    .MapProperty(x => x.State, "state")
                    .MapProperty(x => x.StructuredPayload, "structured.payload")
                    .MapProperty(x => x.Items, "items"));
            });
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IContextWriter>()
            .SetContext(new RichContext(
                true,
                7,
                9,
                123456789,
                1.5f,
                2.5,
                3.5m,
                now,
                id,
                SampleState.Active,
                new Dictionary<string, object?> { ["count"] = 5, ["custom"] = new UnconvertiblePayload("nested") },
                [1, "two", new UnconvertiblePayload("three")]));

        var context = scope.ServiceProvider.GetRequiredService<EvaluationContext>();
        Assert.Equal(true, context.GetValue("is.enabled").AsBoolean);
        Assert.Equal(7, context.GetValue("short.code").AsInteger);
        Assert.Equal(9, context.GetValue("byte.code").AsInteger);
        Assert.Equal(123456789, context.GetValue("long.id").AsInteger);
        Assert.Equal(1.5, context.GetValue("float.value").AsDouble);
        Assert.Equal(2.5, context.GetValue("double.value").AsDouble);
        Assert.Equal(3.5, context.GetValue("decimal.value").AsDouble);
        Assert.Equal(now, context.GetValue("timestamp").AsDateTime);
        Assert.Equal(id.ToString("D"), context.GetValue("correlation.id").AsString);
        Assert.Equal("Active", context.GetValue("state").AsString);
        Assert.NotNull(context.GetValue("structured.payload").AsStructure);
        Assert.NotNull(context.GetValue("items").AsList);
    }

    [Fact]
    public void UseContextR_UseDomain_AppliesToTypedMappings()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx =>
        {
            ctx.AddDomain("tenant-b", domain => domain.Add<UserContext>());
            ctx.AddDomainPolicy(_ => "tenant-b");
        });

        services.AddOpenFeature(featureBuilder =>
        {
            featureBuilder.UseContextR(options =>
            {
                options.UseDomain("tenant-b");
                options.SetTargetingKey<UserContext>(x => x.UserId);
                options.Map<UserContext>(map => map.MapProperty(x => x.Email, "user.email"));
            });
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IContextWriter>()
            .SetContext("tenant-b", new UserContext("u-b", "b@acme.dev", "s"));

        var context = scope.ServiceProvider.GetRequiredService<EvaluationContext>();
        Assert.Equal("u-b", context.TargetingKey);
        Assert.Equal("b@acme.dev", context.GetValue("user.email").AsString);
    }

    [Fact]
    public void ContextRequiredExtensions_ReturnValue_ForAccessorAndSnapshot()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx =>
        {
            ctx.Add<UserContext>();
            ctx.AddDomain("tenant-z", d => d.Add<UserContext>());
            ctx.AddDomainPolicy(_ => "tenant-z");
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IContextWriter>();
        var accessor = scope.ServiceProvider.GetRequiredService<IContextAccessor>();

        writer.SetContext("tenant-z", new UserContext("u-z", "z@acme.dev", "s"));
        var snapshot = accessor.CreateSnapshot("tenant-z", new UserContext("snap-z", "snap@acme.dev", "s2"));

        Assert.Equal("u-z", accessor.GetRequiredContext<UserContext>().UserId);
        Assert.Equal("u-z", accessor.GetRequiredContext<UserContext>("tenant-z").UserId);
        Assert.Equal("snap-z", snapshot.GetRequiredContext<UserContext>("tenant-z").UserId);
    }

    [Fact]
    public void ContextRequiredExtensions_Throws_WhenMissing()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx => ctx.Add<UserContext>());

        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var snapshot = accessor.CreateSnapshot();

        Assert.Throws<InvalidOperationException>(() => accessor.GetRequiredContext<UserContext>());
        Assert.Throws<InvalidOperationException>(() => accessor.GetRequiredContext<UserContext>("missing-domain"));
        Assert.Throws<InvalidOperationException>(() => snapshot.GetRequiredContext<UserContext>());
        Assert.Throws<InvalidOperationException>(() => snapshot.GetRequiredContext<UserContext>("missing-domain"));
    }

    [Fact]
    public void CreateSnapshot_OverloadsAndBeginScope_RestoreAmbientState()
    {
        var services = new ServiceCollection();
        services.AddContextR(ctx =>
        {
            ctx.Add<UserContext>();
            ctx.Add<TenantContext>();
            ctx.AddDomain("tenant-a", d => d.Add<UserContext>());
        });

        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("root-user", "root@acme.dev", "r"));
        writer.SetContext(new TenantContext("root-tenant"));
        writer.SetContext("tenant-a", new UserContext("domain-root", "d@acme.dev", "dr"));

        var fullSnapshot = accessor.CreateSnapshot();
        var defaultSnapshot = accessor.CreateSnapshot(new UserContext("default-snap", "s@acme.dev", "ss"));
        var domainSnapshot = accessor.CreateSnapshot("tenant-a", new UserContext("domain-snap", "ds@acme.dev", "dss"));

        using (fullSnapshot.BeginScope())
        {
            Assert.Equal("root-user", accessor.GetContext<UserContext>()?.UserId);
            Assert.Equal("root-tenant", accessor.GetContext<TenantContext>()?.TenantId);
        }

        using (defaultSnapshot.BeginScope())
        {
            Assert.Equal("default-snap", accessor.GetContext<UserContext>()?.UserId);
        }

        using (domainSnapshot.BeginScope())
        {
            Assert.Equal("domain-snap", accessor.GetContext<UserContext>("tenant-a")?.UserId);
        }

        Assert.Equal("root-user", accessor.GetContext<UserContext>()?.UserId);
        Assert.Equal("domain-root", accessor.GetContext<UserContext>("tenant-a")?.UserId);
    }

    [Fact]
    public void CreateSnapshot_GuardClauses_Throw()
    {
        IContextAccessor? accessor = null;
        var user = new UserContext("u", "u@acme.dev", "s");

        Assert.Throws<ArgumentNullException>(() => accessor!.CreateSnapshot());
        Assert.Throws<ArgumentNullException>(() => accessor!.CreateSnapshot(user));
        Assert.Throws<ArgumentNullException>(() => accessor!.CreateSnapshot("d", user));
    }

    [Fact]
    public void AddContextR_DomainOnlyWithoutDefaultOrPolicy_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddContextR(builder =>
            {
                builder.AddDomain("tenant-x", domain => domain.Add<UserContext>());
            }));
    }

    [Fact]
    public void AddContextR_DomainOnlyWithDefaultPolicy_DoesNotThrow()
    {
        var services = new ServiceCollection();

        var exception = Record.Exception(() =>
            services.AddContextR(builder =>
            {
                builder.AddDomain("tenant-x", domain => domain.Add<UserContext>());
                builder.AddDomainPolicy(_ => "tenant-x");
            }));

        Assert.Null(exception);
    }

    [Fact]
    public void AddContextR_RegistrationBuilders_ExposeServicesAndDomain()
    {
        var services = new ServiceCollection();
        string? defaultDomain = "not-set";
        string? tenantDomain = null;
        IServiceCollection? defaultServices = null;
        IServiceCollection? tenantServices = null;

        services.AddContextR(builder =>
        {
            builder.Add<UserContext>(reg =>
            {
                defaultDomain = reg.Domain;
                defaultServices = reg.Services;
            });

            builder.AddDomain("tenant-y", domain =>
            {
                domain.Add<UserContext>(reg =>
                {
                    tenantDomain = reg.Domain;
                    tenantServices = reg.Services;
                });
            });

            builder.AddDomainPolicy(_ => "tenant-y");
        });

        Assert.Null(defaultDomain);
        Assert.Equal("tenant-y", tenantDomain);
        Assert.Same(services, defaultServices);
        Assert.Same(services, tenantServices);
    }

    private sealed record UserContext(string UserId, string Email, string Secret);

    private sealed record TenantContext(string TenantId);
    private sealed record NullableContext(string? OptionalValue);
    private sealed record UnsupportedContext(UnconvertiblePayload Payload);
    private sealed record UnconvertiblePayload(string Value)
    {
        public override string ToString() => $"payload:{Value}";
    }

    private sealed record RichContext(
        bool IsEnabled,
        short ShortCode,
        byte ByteCode,
        long LongId,
        float FloatValue,
        double DoubleValue,
        decimal DecimalValue,
        DateTime Timestamp,
        Guid CorrelationId,
        SampleState State,
        Dictionary<string, object?> StructuredPayload,
        List<object?> Items);

    private enum SampleState
    {
        Unknown = 0,
        Active = 1
    }
}
