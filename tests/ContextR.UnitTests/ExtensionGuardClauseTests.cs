using Microsoft.Extensions.DependencyInjection;

namespace ContextR.UnitTests;

public sealed class ExtensionGuardClauseTests
{
    [Fact]
    public void CreateSnapshot_Throws_WhenAccessorIsNull()
    {
        IContextAccessor? accessor = null;

        Assert.Throws<ArgumentNullException>(() => accessor!.CreateSnapshot());
    }

    [Fact]
    public void CreateSnapshotGeneric_Throws_WhenAccessorIsNull()
    {
        IContextAccessor? accessor = null;

        Assert.Throws<ArgumentNullException>(() => accessor!.CreateSnapshot(new UserContext("u1")));
    }

    [Fact]
    public void CreateSnapshotGeneric_Throws_WhenContextIsNull()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        Assert.Throws<ArgumentNullException>(() =>
            accessor.CreateSnapshot<UserContext>(null!));
    }

    [Fact]
    public void GetRequiredContextAccessor_Throws_WhenAccessorIsNull()
    {
        IContextAccessor? accessor = null;

        Assert.Throws<ArgumentNullException>(() => accessor!.GetRequiredContext<UserContext>());
    }

    [Fact]
    public void GetRequiredContextSnapshot_Throws_WhenSnapshotIsNull()
    {
        IContextSnapshot? snapshot = null;

        Assert.Throws<ArgumentNullException>(() => snapshot!.GetRequiredContext<UserContext>());
    }

    [Fact]
    public void GetRequiredContextAccessor_Throws_WhenMissing()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();

        Assert.Throws<InvalidOperationException>(() => accessor.GetRequiredContext<UserContext>());
    }

    [Fact]
    public void GetRequiredContextSnapshot_Throws_WhenMissing()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var snapshot = accessor.CreateSnapshot();

        Assert.Throws<InvalidOperationException>(() => snapshot.GetRequiredContext<UserContext>());
    }

    [Fact]
    public void GetRequiredContext_Works_ForAccessorAndSnapshot()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("u1"));
        var snapshot = accessor.CreateSnapshot();
        writer.SetContext(new UserContext("u2"));

        Assert.Equal("u2", accessor.GetRequiredContext<UserContext>().UserId);
        Assert.Equal("u1", snapshot.GetRequiredContext<UserContext>().UserId);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder => builder.Add<UserContext>());
        return services.BuildServiceProvider();
    }

    private sealed record UserContext(string UserId);
}
