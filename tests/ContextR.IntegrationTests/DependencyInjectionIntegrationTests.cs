using Microsoft.Extensions.DependencyInjection;

namespace ContextR.IntegrationTests;

public sealed class DependencyInjectionIntegrationTests
{
    [Fact]
    public void AddContextR_RegistersSingletonAccessorAndWriter()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        var rootAccessor = provider.GetRequiredService<IContextAccessor>();
        var scopedAccessor = scope.ServiceProvider.GetRequiredService<IContextAccessor>();
        var rootWriter = provider.GetRequiredService<IContextWriter>();
        var scopedWriter = scope.ServiceProvider.GetRequiredService<IContextWriter>();

        Assert.Same(rootAccessor, scopedAccessor);
        Assert.Same(rootWriter, scopedWriter);
    }

    [Fact]
    public void AddContextR_RegistersScopedSnapshot_CapturedAtResolutionTime()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        writer.SetContext(new UserContext("user-before-scope1"));

        using var scope1 = provider.CreateScope();
        var snapshot1a = scope1.ServiceProvider.GetRequiredService<IContextSnapshot>();

        writer.SetContext(new UserContext("user-after-scope1"));
        var snapshot1b = scope1.ServiceProvider.GetRequiredService<IContextSnapshot>();

        using var scope2 = provider.CreateScope();
        var snapshot2 = scope2.ServiceProvider.GetRequiredService<IContextSnapshot>();

        Assert.Same(snapshot1a, snapshot1b);
        Assert.Equal("user-before-scope1", snapshot1a.GetContext<UserContext>()?.UserId);
        Assert.Equal("user-after-scope1", snapshot2.GetContext<UserContext>()?.UserId);
        Assert.Equal("user-after-scope1", accessor.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void AddContextR_Builder_IsChainable_AndInvokesTypedConfiguration()
    {
        var contextBuildersInvoked = 0;

        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder
                .Add<UserContext>(_ => contextBuildersInvoked++)
                .Add<TenantContext>(_ => contextBuildersInvoked++);
        });

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IContextAccessor>();

        Assert.Equal(2, contextBuildersInvoked);
    }

    [Fact]
    public void GetRequiredContext_ThrowsWhenMissing_AndReturnsWhenPresent()
    {
        using var provider = CreateProvider();
        var accessor = provider.GetRequiredService<IContextAccessor>();
        var writer = provider.GetRequiredService<IContextWriter>();

        Assert.Throws<InvalidOperationException>(() => accessor.GetRequiredContext<UserContext>());

        writer.SetContext(new UserContext("user-required"));
        Assert.Equal("user-required", accessor.GetRequiredContext<UserContext>().UserId);
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

    private sealed record UserContext(string UserId);
    private sealed record TenantContext(string TenantId);
}
