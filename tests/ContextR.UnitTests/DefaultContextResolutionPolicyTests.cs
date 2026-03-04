using ContextR.Resolution;
using ContextR.Resolution.Internal;

namespace ContextR.UnitTests;

public sealed class DefaultContextResolutionPolicyTests
{
    private readonly DefaultContextResolutionPolicy<TestContext> _sut = new();

    [Fact]
    public void Resolve_Throws_WhenContextIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.Resolve(null!));
    }

    [Fact]
    public void Resolve_ReturnsNone_WhenResolvedAndPropagatedAreMissing()
    {
        var result = _sut.Resolve(new ContextResolutionPolicyContext<TestContext>
        {
            ResolutionContext = new ContextResolutionContext { Boundary = ContextIngressBoundary.External },
            ResolvedContext = null,
            PropagatedContext = null
        });

        Assert.Null(result.Context);
        Assert.Equal(ContextResolutionSource.None, result.Source);
    }

    [Fact]
    public void Resolve_ReturnsPropagated_WhenResolverIsMissing()
    {
        var propagated = new TestContext("prop");

        var result = _sut.Resolve(new ContextResolutionPolicyContext<TestContext>
        {
            ResolutionContext = new ContextResolutionContext { Boundary = ContextIngressBoundary.External },
            ResolvedContext = null,
            PropagatedContext = propagated
        });

        Assert.Same(propagated, result.Context);
        Assert.Equal(ContextResolutionSource.Propagated, result.Source);
    }

    [Fact]
    public void Resolve_ReturnsResolver_WhenPropagatedIsMissing()
    {
        var resolved = new TestContext("resolved");

        var result = _sut.Resolve(new ContextResolutionPolicyContext<TestContext>
        {
            ResolutionContext = new ContextResolutionContext { Boundary = ContextIngressBoundary.Internal },
            ResolvedContext = resolved,
            PropagatedContext = null
        });

        Assert.Same(resolved, result.Context);
        Assert.Equal(ContextResolutionSource.Resolver, result.Source);
    }

    private sealed record TestContext(string Value);
}
