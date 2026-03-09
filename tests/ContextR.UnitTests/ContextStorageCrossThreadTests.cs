using Microsoft.Extensions.DependencyInjection;

namespace ContextR.UnitTests;

/// <summary>
/// Verifies that ContextStorage.Set does NOT corrupt context in child execution contexts.
/// The fix replaces shared ContextHolder mutation with a clean AsyncLocal value assignment,
/// ensuring proper isolation between parent and child flows.
/// </summary>
public sealed class ContextStorageCrossThreadTests
{
    [Fact]
    public async Task SetContext_OnParent_DoesNotCorruptInheritedContext_InChildTask()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("initial"));

        var barrier = new TaskCompletionSource();
        var childReady = new TaskCompletionSource();

        var childTask = Task.Run(async () =>
        {
            var before = accessor.GetContext<UserContext>()?.UserId;
            childReady.SetResult();

            await barrier.Task;

            var after = accessor.GetContext<UserContext>()?.UserId;
            return (before, after);
        });

        await childReady.Task;

        writer.SetContext(new UserContext("overwritten"));
        barrier.SetResult();

        var (before, after) = await childTask;

        Assert.Equal("initial", before);
        Assert.Equal("initial", after);
    }

    [Fact]
    public async Task SetContext_OnParent_DoesNotCorrupt_WhenUsingScopeRestore()
    {
        using var provider = CreateProvider();
        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext(new UserContext("initial"));
        var snapshot = accessor.CreateSnapshot(new UserContext("scoped"));

        var childResult = await Task.Run(() =>
        {
            using (snapshot.BeginScope())
            {
                return accessor.GetContext<UserContext>()?.UserId;
            }
        });

        Assert.Equal("scoped", childResult);
        Assert.Equal("initial", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public async Task SetContext_OnParent_DoesNotCorruptDomainContext_InChildTask()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<UserContext>();
            builder.AddDomain("web-api", d => d.Add<UserContext>());
        });
        using var provider = services.BuildServiceProvider();

        var writer = provider.GetRequiredService<IContextWriter>();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        writer.SetContext("web-api", new UserContext("domain-initial"));

        var barrier = new TaskCompletionSource();
        var childReady = new TaskCompletionSource();

        var childTask = Task.Run(async () =>
        {
            var before = accessor.GetContext<UserContext>("web-api")?.UserId;
            childReady.SetResult();
            await barrier.Task;
            var after = accessor.GetContext<UserContext>("web-api")?.UserId;
            return (before, after);
        });

        await childReady.Task;

        writer.SetContext("web-api", new UserContext("domain-overwritten"));
        barrier.SetResult();

        var (before, after) = await childTask;

        Assert.Equal("domain-initial", before);
        Assert.Equal("domain-initial", after);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder => builder.Add<UserContext>());
        return services.BuildServiceProvider();
    }

    private sealed record UserContext(string UserId);
}
