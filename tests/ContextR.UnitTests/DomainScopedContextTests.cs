using Microsoft.Extensions.DependencyInjection;

namespace ContextR.UnitTests;

public sealed class DomainScopedContextTests
{
    [Fact]
    public void Domain_GetSet_RoundTrip()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", new UserContext("alice"));

        Assert.Equal("alice", accessor.GetContext<UserContext>("web-api")?.UserId);
        Assert.Null(accessor.GetContext<UserContext>());
    }

    [Fact]
    public void Domain_IsolatesValues_FromDefault()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("default-user"));
        writer.SetContext("web-api", new UserContext("web-user"));

        Assert.Equal("default-user", accessor.GetContext<UserContext>()?.UserId);
        Assert.Equal("web-user", accessor.GetContext<UserContext>("web-api")?.UserId);
    }

    [Fact]
    public void Domain_MultipleDomains_AreIsolated()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
            builder.AddDomain("grpc", domain => domain.Add<UserContext>());
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", new UserContext("web-user"));
        writer.SetContext("grpc", new UserContext("grpc-user"));

        Assert.Equal("web-user", accessor.GetContext<UserContext>("web-api")?.UserId);
        Assert.Equal("grpc-user", accessor.GetContext<UserContext>("grpc")?.UserId);
        Assert.Null(accessor.GetContext<UserContext>());
    }

    [Fact]
    public void DefaultDomainSelector_DelegatesParameterlessCalls()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
            builder.AddDomainPolicy(p => p.DefaultDomainSelector = _ => "web-api");
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("alice"));

        Assert.Equal("alice", accessor.GetContext<UserContext>()?.UserId);
        Assert.Equal("alice", accessor.GetContext<UserContext>("web-api")?.UserId);
    }

    [Fact]
    public void DefaultDomainSelector_ReceivesServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton("web-api");
        services.AddContextR(builder =>
        {
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
            builder.AddDomainPolicy(sp => sp.GetRequiredService<string>());
        });

        using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("resolved-via-sp"));

        Assert.Equal("resolved-via-sp", accessor.GetContext<UserContext>("web-api")?.UserId);
    }

    [Fact]
    public void AddDomainPolicySelector_Throws_WhenSelectorIsNull()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddContextR(builder =>
            {
                builder.Add<UserContext>();
                builder.AddDomainPolicy((Func<IServiceProvider, string?>)null!);
            }));
    }

    [Fact]
    public void BuilderValidation_Throws_WhenDomainWithoutDefaultOrPolicy()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddContextR(builder =>
            {
                builder.AddDomain("web-api", domain => domain.Add<UserContext>());
            }));

        Assert.Contains("DefaultDomainSelector", ex.Message);
    }

    [Fact]
    public void BuilderValidation_Passes_WhenDomainWithDefaultRegistration()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IContextAccessor>());
    }

    [Fact]
    public void BuilderValidation_Passes_WhenDomainWithPolicy()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
            builder.AddDomainPolicy(p => p.DefaultDomainSelector = _ => "web-api");
        });

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IContextAccessor>());
    }

    [Fact]
    public void AddDomain_Throws_WhenDomainIsNull()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddContextR(builder =>
            {
                builder.Add<UserContext>();
                builder.AddDomain(null!, domain => domain.Add<UserContext>());
            }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void AddDomain_Throws_WhenDomainIsEmptyOrWhitespace(string domain)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddContextR(builder =>
            {
                builder.Add<UserContext>();
                builder.AddDomain(domain, d => d.Add<UserContext>());
            }));
    }

    [Fact]
    public void AddDomain_Throws_WhenConfigureIsNull()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddContextR(builder =>
            {
                builder.Add<UserContext>();
                builder.AddDomain("web-api", null!);
            }));
    }

    [Fact]
    public void AddDomainPolicy_Throws_WhenConfigureIsNull()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddContextR(builder =>
            {
                builder.Add<UserContext>();
                builder.AddDomainPolicy(null!);
            }));
    }

    [Fact]
    public void Snapshot_CapturesDomainValues()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("default-user"));
        writer.SetContext("web-api", new UserContext("web-user"));

        var snapshot = accessor.CreateSnapshot();

        writer.SetContext(new UserContext("changed"));
        writer.SetContext("web-api", new UserContext("changed"));

        Assert.Equal("default-user", snapshot.GetContext<UserContext>()?.UserId);
        Assert.Equal("web-user", snapshot.GetContext<UserContext>("web-api")?.UserId);
    }

    [Fact]
    public void Snapshot_CreateSnapshotWithDomain_ContainsDomainValue()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var accessor = provider.GetRequiredService<IContextAccessor>();

        var snapshot = accessor.CreateSnapshot("web-api", new UserContext("scoped"));

        Assert.Equal("scoped", snapshot.GetContext<UserContext>("web-api")?.UserId);
        Assert.Null(snapshot.GetContext<UserContext>());
    }

    [Fact]
    public void Snapshot_DefaultDomainSelector_PropagatedToSnapshot()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
            builder.AddDomainPolicy(p => p.DefaultDomainSelector = _ => "web-api");
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("alice"));
        var snapshot = accessor.CreateSnapshot();

        Assert.Equal("alice", snapshot.GetContext<UserContext>()?.UserId);
        Assert.Equal("alice", snapshot.GetContext<UserContext>("web-api")?.UserId);
    }

    [Fact]
    public void Scope_RestoresDomainValues()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", new UserContext("parent"));
        var snapshot = accessor.CreateSnapshot("web-api", new UserContext("scoped"));

        using (snapshot.BeginScope())
        {
            Assert.Equal("scoped", accessor.GetContext<UserContext>("web-api")?.UserId);
        }

        Assert.Equal("parent", accessor.GetContext<UserContext>("web-api")?.UserId);
    }

    [Fact]
    public async Task Scope_DomainValues_InTask_DoNotClearParent()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", new UserContext("parent"));
        var childSnapshot = accessor.CreateSnapshot("web-api", new UserContext("child"));

        var childResult = await Task.Run(() =>
        {
            using (childSnapshot.BeginScope())
            {
                return accessor.GetContext<UserContext>("web-api")?.UserId;
            }
        });

        Assert.Equal("child", childResult);
        Assert.Equal("parent", accessor.GetContext<UserContext>("web-api")?.UserId);
    }

    [Fact]
    public void GetRequiredContext_Domain_ThrowsWhenMissing()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var accessor = provider.GetRequiredService<IContextAccessor>();

        var ex = Assert.Throws<InvalidOperationException>(
            () => accessor.GetRequiredContext<UserContext>("web-api"));
        Assert.Contains("web-api", ex.Message);
    }

    [Fact]
    public void GetRequiredContext_Domain_ReturnsValueWhenPresent()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", new UserContext("alice"));

        Assert.Equal("alice", accessor.GetRequiredContext<UserContext>("web-api").UserId);
    }

    [Fact]
    public void GetRequiredContext_Snapshot_Domain_ThrowsWhenMissing()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var accessor = provider.GetRequiredService<IContextAccessor>();
        var snapshot = accessor.CreateSnapshot();

        var ex = Assert.Throws<InvalidOperationException>(
            () => snapshot.GetRequiredContext<UserContext>("web-api"));
        Assert.Contains("web-api", ex.Message);
    }

    [Fact]
    public void AddDomain_IsChainable()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            var result = builder
                .AddDomain("a", d => d.Add<UserContext>())
                .AddDomain("b", d => d.Add<UserContext>())
                .AddDomainPolicy(p => p.DefaultDomainSelector = _ => "a");

            Assert.Same(builder, result);
        });
    }

    [Fact]
    public void DomainBuilder_Add_IsChainable()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain =>
            {
                var result = domain
                    .Add<UserContext>()
                    .Add<TenantContext>();

                Assert.Same(domain, result);
            });
        });
    }

    [Fact]
    public void BackwardCompatibility_DefaultOnlyRegistration_StillWorks()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.Add<TenantContext>();
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("u1"));
        writer.SetContext(new TenantContext("t1"));

        Assert.Equal("u1", accessor.GetContext<UserContext>()?.UserId);
        Assert.Equal("t1", accessor.GetContext<TenantContext>()?.TenantId);

        var snapshot = accessor.CreateSnapshot();
        writer.SetContext(new UserContext("u2"));

        Assert.Equal("u1", snapshot.GetContext<UserContext>()?.UserId);
        Assert.Equal("u2", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void SetContextNull_ClearsDomainValue()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", new UserContext("alice"));
        Assert.Equal("alice", accessor.GetContext<UserContext>("web-api")?.UserId);

        writer.SetContext<UserContext>("web-api", null);
        Assert.Null(accessor.GetContext<UserContext>("web-api"));
    }

    [Fact]
    public void SetContextNull_ClearsDefaultValue()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("alice"));
        Assert.Equal("alice", accessor.GetContext<UserContext>()?.UserId);

        writer.SetContext<UserContext>(null);
        Assert.Null(accessor.GetContext<UserContext>());
    }

    [Fact]
    public void DefaultDomainSelector_ReturningNull_UsesDomainlessSlot()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomainPolicy(p => p.DefaultDomainSelector = _ => null);
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("domainless"));

        Assert.Equal("domainless", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void DomainBuilder_Add_InvokesConfigureCallback()
    {
        var invoked = false;

        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain =>
            {
                domain.Add<UserContext>(_ => invoked = true);
            });
        });

        Assert.True(invoked);
    }

    [Fact]
    public void Scope_DoubleDispose_IsIdempotent()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("parent"));
        var snapshot = accessor.CreateSnapshot(new UserContext("scoped"));
        var scope = snapshot.BeginScope();

        Assert.Equal("scoped", accessor.GetContext<UserContext>()?.UserId);

        scope.Dispose();
        Assert.Equal("parent", accessor.GetContext<UserContext>()?.UserId);

        scope.Dispose();
        Assert.Equal("parent", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void Scope_RestoresToNull_WhenNoPreviousValue()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var accessor = provider.GetRequiredService<IContextAccessor>();

        Assert.Null(accessor.GetContext<UserContext>("web-api"));

        var snapshot = accessor.CreateSnapshot("web-api", new UserContext("scoped"));

        using (snapshot.BeginScope())
        {
            Assert.Equal("scoped", accessor.GetContext<UserContext>("web-api")?.UserId);
        }

        Assert.Null(accessor.GetContext<UserContext>("web-api"));
    }

    [Fact]
    public void Snapshot_GetContext_ReturnsNull_ForUnsetDomain()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("default-only"));
        var snapshot = accessor.CreateSnapshot();

        Assert.Equal("default-only", snapshot.GetContext<UserContext>()?.UserId);
        Assert.Null(snapshot.GetContext<UserContext>("web-api"));
        Assert.Null(snapshot.GetContext<UserContext>("nonexistent"));
    }

    [Fact]
    public void GetRequiredContext_Snapshot_Domain_ReturnsValueWhenPresent()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", new UserContext("web-user"));
        var snapshot = accessor.CreateSnapshot();

        Assert.Equal("web-user", snapshot.GetRequiredContext<UserContext>("web-api").UserId);
    }

    [Fact]
    public void MultipleContextTypes_AcrossMultipleDomains()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.Add<TenantContext>();
            builder.AddDomain("web-api", domain =>
            {
                domain.Add<UserContext>();
                domain.Add<TenantContext>();
            });
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("default-user"));
        writer.SetContext(new TenantContext("default-tenant"));
        writer.SetContext("web-api", new UserContext("web-user"));
        writer.SetContext("web-api", new TenantContext("web-tenant"));

        Assert.Equal("default-user", accessor.GetContext<UserContext>()?.UserId);
        Assert.Equal("default-tenant", accessor.GetContext<TenantContext>()?.TenantId);
        Assert.Equal("web-user", accessor.GetContext<UserContext>("web-api")?.UserId);
        Assert.Equal("web-tenant", accessor.GetContext<TenantContext>("web-api")?.TenantId);
    }

    [Fact]
    public void OverwriteExistingValue_ReplacesCorrectly()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", new UserContext("first"));
        Assert.Equal("first", accessor.GetContext<UserContext>("web-api")?.UserId);

        writer.SetContext("web-api", new UserContext("second"));
        Assert.Equal("second", accessor.GetContext<UserContext>("web-api")?.UserId);
    }

    [Fact]
    public void BuilderValidation_Passes_WhenNoDomainsRegistered()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            builder.Add<UserContext>();
        });

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IContextAccessor>());
    }

    [Fact]
    public void Snapshot_MixedDefaultAndDomain_CapturesBoth()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("default"));
        writer.SetContext("web-api", new UserContext("web"));

        var snapshot = accessor.CreateSnapshot();

        writer.SetContext(new UserContext("changed-default"));
        writer.SetContext("web-api", new UserContext("changed-web"));

        Assert.Equal("default", snapshot.GetContext<UserContext>()?.UserId);
        Assert.Equal("web", snapshot.GetContext<UserContext>("web-api")?.UserId);
    }

    [Fact]
    public void Scope_MixedDefaultAndDomain_RestoresBoth()
    {
        using var provider = CreateProvider(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", domain => domain.Add<UserContext>());
        });

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("parent-default"));
        writer.SetContext("web-api", new UserContext("parent-web"));

        var snapshot = accessor.CreateSnapshot();

        writer.SetContext(new UserContext("changed-default"));
        writer.SetContext("web-api", new UserContext("changed-web"));

        using (snapshot.BeginScope())
        {
            Assert.Equal("parent-default", accessor.GetContext<UserContext>()?.UserId);
            Assert.Equal("parent-web", accessor.GetContext<UserContext>("web-api")?.UserId);
        }

        Assert.Equal("changed-default", accessor.GetContext<UserContext>()?.UserId);
        Assert.Equal("changed-web", accessor.GetContext<UserContext>("web-api")?.UserId);
    }

    private static ServiceProvider CreateProvider(Action<IContextBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddContextR(configure);
        return services.BuildServiceProvider();
    }

    private sealed record UserContext(string UserId);
    private sealed record TenantContext(string TenantId);
}
