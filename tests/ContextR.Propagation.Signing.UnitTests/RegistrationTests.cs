using System.Security.Cryptography;
using ContextR.Propagation.Mapping;
using ContextR.Propagation.Signing.UnitTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Propagation.Signing.UnitTests;

public sealed class RegistrationTests
{
    [Fact]
    public void UseContextSigning_NullBuilder_Throws()
    {
        IContextRegistrationBuilder<TestContext>? builder = null;
        Assert.Throws<ArgumentNullException>(() =>
            builder!.UseContextSigning<TestContext>(o => o.Key = new byte[32]));
    }

    [Fact]
    public void UseContextSigning_NullConfigure_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
        {
            services.AddContextR(ctx =>
            {
                ctx.Add<TestContext>(reg => reg
                    .MapProperty(c => c.TenantId, "X-Tenant-Id")
                    .UseContextSigning<TestContext>(null!));
            });
        });
    }

    [Fact]
    public void UseContextSigning_NoKeysAndNoKeyId_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
        {
            services.AddContextR(ctx =>
            {
                ctx.Add<TestContext>(reg => reg
                    .MapProperty(c => c.TenantId, "X-Tenant-Id")
                    .UseContextSigning<TestContext>(o => { }));
            });
        });
    }

    [Fact]
    public void UseContextSigning_InlineKey_RegistersPropagator()
    {
        var services = new ServiceCollection();

        services.AddContextR(ctx =>
        {
            ctx.Add<TestContext>(reg => reg
                .MapProperty(c => c.TenantId, "X-Tenant-Id")
                .UseContextSigning<TestContext>(o => o.Key = RandomNumberGenerator.GetBytes(32)));
        });

        var sp = services.BuildServiceProvider();
        var propagator = sp.GetRequiredService<IContextPropagator<TestContext>>();

        Assert.NotNull(propagator);
    }

    [Fact]
    public void UseContextSigning_InlineKey_DoesNotRequireKeyProvider()
    {
        var services = new ServiceCollection();

        services.AddContextR(ctx =>
        {
            ctx.Add<TestContext>(reg => reg
                .MapProperty(c => c.TenantId, "X-Tenant-Id")
                .UseContextSigning<TestContext>(o => o.Key = RandomNumberGenerator.GetBytes(32)));
        });

        var sp = services.BuildServiceProvider();
        var propagator = sp.GetRequiredService<IContextPropagator<TestContext>>();

        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        propagator.Inject(new TestContext { TenantId = "acme" }, headers,
            static (dict, key, value) => dict[key] = value);

        Assert.True(headers.ContainsKey("X-Context-Signature"));
    }

    [Fact]
    public void UseContextSigning_KeyId_WithProvider_RegistersPropagator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISigningKeyProvider>(new StaticSigningKeyProvider("k1", 1));

        services.AddContextR(ctx =>
        {
            ctx.Add<TestContext>(reg => reg
                .MapProperty(c => c.TenantId, "X-Tenant-Id")
                .UseContextSigning<TestContext>(o => o.KeyId = "k1"));
        });

        var sp = services.BuildServiceProvider();
        var propagator = sp.GetRequiredService<IContextPropagator<TestContext>>();

        Assert.NotNull(propagator);
    }

    [Fact]
    public void UseContextSigning_KeyId_MissingProvider_Throws_AtResolution()
    {
        var services = new ServiceCollection();

        services.AddContextR(ctx =>
        {
            ctx.Add<TestContext>(reg => reg
                .MapProperty(c => c.TenantId, "X-Tenant-Id")
                .UseContextSigning<TestContext>(o => o.KeyId = "k1"));
        });

        var sp = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() =>
            sp.GetRequiredService<IContextPropagator<TestContext>>());
    }
}
