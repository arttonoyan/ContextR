using Microsoft.Extensions.DependencyInjection;

namespace ContextR.UnitTests;

public sealed class BuilderAndRegistrationTests
{
    [Fact]
    public void AddContextR_Throws_WhenServicesIsNull()
    {
        IServiceCollection? services = null;

        Assert.Throws<ArgumentNullException>(() =>
            ContextRServiceCollectionExtensions.AddContextR(
                services!,
                _ => { }));
    }

    [Fact]
    public void AddContextR_Throws_WhenConfigureIsNull()
    {
        var services = new ServiceCollection();
        Action<IContextBuilder>? configure = null;

        Assert.Throws<ArgumentNullException>(() =>
            services.AddContextR(configure!));
    }

    [Fact]
    public void Builder_Add_IsChainable_WhenNoTypedConfigureProvided()
    {
        var services = new ServiceCollection();

        services.AddContextR(builder =>
        {
            var result = builder
                .Add<UserContext>()
                .Add<TenantContext>();

            Assert.Same(builder, result);
        });
    }

    [Fact]
    public void Builder_Add_InvokesTypedConfigure_ForEachRegisteredContext()
    {
        var services = new ServiceCollection();
        var invoked = 0;

        services.AddContextR(builder =>
        {
            builder
                .Add<UserContext>(_ => invoked++)
                .Add<TenantContext>(_ => invoked++);
        });

        Assert.Equal(2, invoked);
    }

    [Fact]
    public void AddContextR_RegistersExpectedServices()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder => builder.Add<UserContext>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(provider.GetRequiredService<IContextAccessor>());
        Assert.NotNull(provider.GetRequiredService<IContextWriter>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IContextSnapshot>());
    }

    private sealed record UserContext(string UserId = "u1");
    private sealed record TenantContext(string TenantId = "t1");
}
