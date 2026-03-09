using Microsoft.Extensions.DependencyInjection;

namespace ContextR.UnitTests;

/// <summary>
/// Verifies that DefaultDomainSelector is evaluated per-call, allowing runtime domain
/// changes to take effect immediately for parameterless context operations.
/// </summary>
public sealed class DefaultContextAccessorDomainCachingTests
{
    [Fact]
    public void DefaultDomainSelector_IsEvaluatedPerAccess()
    {
        var selectorCallCount = 0;
        var domainSource = "domain-a";

        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.AddDomain("domain-a", d => d.Add<UserContext>());
            builder.AddDomain("domain-b", d => d.Add<UserContext>());
            builder.AddDomainPolicy(sp =>
            {
                selectorCallCount++;
                return domainSource;
            });
        });

        using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("domain-a", new UserContext("alice"));
        writer.SetContext("domain-b", new UserContext("bob"));

        Assert.Equal("alice", accessor.GetContext<UserContext>()?.UserId);
        var callsAfterFirst = selectorCallCount;
        Assert.True(callsAfterFirst >= 1);

        domainSource = "domain-b";

        Assert.Equal("bob", accessor.GetContext<UserContext>()?.UserId);
        Assert.True(selectorCallCount > callsAfterFirst);
    }

    [Fact]
    public void DefaultDomainSelector_ReEvaluatedValue_PropagatesTo_ScopedSnapshots()
    {
        var domainSource = "domain-a";

        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.AddDomain("domain-a", d => d.Add<UserContext>());
            builder.AddDomain("domain-b", d => d.Add<UserContext>());
            builder.AddDomainPolicy(_ => domainSource);
        });

        using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        writer.SetContext("domain-a", new UserContext("alice"));
        writer.SetContext("domain-b", new UserContext("bob"));

        using (var scope1 = provider.CreateScope())
        {
            var snapshot1 = scope1.ServiceProvider.GetRequiredService<IContextSnapshot>();
            Assert.Equal("alice", snapshot1.GetContext<UserContext>()?.UserId);
        }

        domainSource = "domain-b";

        using (var scope2 = provider.CreateScope())
        {
            var snapshot2 = scope2.ServiceProvider.GetRequiredService<IContextSnapshot>();
            Assert.Equal("bob", snapshot2.GetContext<UserContext>()?.UserId);
        }
    }

    [Fact]
    public void DefaultDomainSelector_SetContext_WritesToCurrentDomain()
    {
        var domainSource = "domain-a";

        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.AddDomain("domain-a", d => d.Add<UserContext>());
            builder.AddDomain("domain-b", d => d.Add<UserContext>());
            builder.AddDomainPolicy(_ => domainSource);
        });

        using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        domainSource = "domain-b";

        writer.SetContext(new UserContext("should-go-to-domain-b"));

        Assert.Null(accessor.GetContext<UserContext>("domain-a"));
        Assert.Equal("should-go-to-domain-b", accessor.GetContext<UserContext>("domain-b")?.UserId);
    }

    private sealed record UserContext(string UserId);
}
