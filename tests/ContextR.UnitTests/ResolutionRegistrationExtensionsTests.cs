using ContextR.Resolution;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.UnitTests;

public sealed class ResolutionRegistrationExtensionsTests
{
    [Fact]
    public void AddResolution_Throws_WhenBuilderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ContextRResolutionRegistrationExtensions.AddResolution<TestContext>(null!, _ => { }));
    }

    [Fact]
    public void AddResolution_Throws_WhenConfigureIsNull()
    {
        var services = new ServiceCollection();
        IContextRegistrationBuilder<TestContext>? capturedBuilder = null;
        services.AddContextR(builder => builder.Add<TestContext>(reg => capturedBuilder = reg));

        Assert.NotNull(capturedBuilder);
        Assert.Throws<ArgumentNullException>(() =>
            capturedBuilder!.AddResolution(null!));
    }

    [Fact]
    public void UseResolver_Throws_WhenDelegateOrFactoryIsNull()
    {
        var services = new ServiceCollection();
        IContextRegistrationBuilder<TestContext>? capturedBuilder = null;
        services.AddContextR(builder => builder.Add<TestContext>(reg => capturedBuilder = reg));

        Assert.NotNull(capturedBuilder);
        Assert.Throws<ArgumentNullException>(() =>
            capturedBuilder!.UseResolver<TestContext>((Func<ContextResolutionContext, TestContext?>)null!));
        Assert.Throws<ArgumentNullException>(() =>
            capturedBuilder.UseResolver<TestContext>((Func<IServiceProvider, IContextResolver<TestContext>>)null!));
    }

    [Fact]
    public void UseResolutionPolicy_Throws_WhenDelegateOrFactoryIsNull()
    {
        var services = new ServiceCollection();
        IContextRegistrationBuilder<TestContext>? capturedBuilder = null;
        services.AddContextR(builder => builder.Add<TestContext>(reg => capturedBuilder = reg));

        Assert.NotNull(capturedBuilder);
        Assert.Throws<ArgumentNullException>(() =>
            capturedBuilder!.UseResolutionPolicy<TestContext>(
                (Func<ContextResolutionPolicyContext<TestContext>, ContextResolutionResult<TestContext>>)null!));
        Assert.Throws<ArgumentNullException>(() =>
            capturedBuilder.UseResolutionPolicy<TestContext>((Func<IServiceProvider, IContextResolutionPolicy<TestContext>>)null!));
    }

    [Fact]
    public void AddResolution_AllOverloads_RegisterAndResolve()
    {
        var services = new ServiceCollection();
        services.AddContextR(builder =>
        {
            builder.Add<TestContext>(reg => reg
                .AddResolution(r => r
                    .UseResolver<ConstantResolver>()
                    .UseResolver(_ => new TestContext("delegate-resolver"))
                    .UseResolver(_ => new ConstantResolver())
                    .UseResolutionPolicy<PreferResolvedPolicy>()
                    .UseResolutionPolicy(ctx => new ContextResolutionResult<TestContext>
                    {
                        Context = ctx.ResolvedContext,
                        Source = ContextResolutionSource.Policy
                    })
                    .UseResolutionPolicy(_ => new PreferResolvedPolicy())));
        });

        using var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IContextResolutionOrchestrator<TestContext>>();
        var result = orchestrator.Resolve(new ContextResolutionContext { Boundary = ContextIngressBoundary.External });

        Assert.NotNull(result.Context);
        Assert.Equal("typed-resolver", result.Context!.Value);
        Assert.Equal(ContextResolutionSource.Policy, result.Source);
    }

    private sealed record TestContext(string Value);

    private sealed class ConstantResolver : IContextResolver<TestContext>
    {
        public TestContext? Resolve(ContextResolutionContext context) => new("typed-resolver");
    }

    private sealed class PreferResolvedPolicy : IContextResolutionPolicy<TestContext>
    {
        public ContextResolutionResult<TestContext> Resolve(ContextResolutionPolicyContext<TestContext> context)
            => new()
            {
                Context = context.ResolvedContext,
                Source = ContextResolutionSource.Policy
            };
    }
}
