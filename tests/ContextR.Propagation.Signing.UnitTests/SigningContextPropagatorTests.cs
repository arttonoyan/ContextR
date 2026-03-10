using System.Security.Cryptography;
using ContextR.Propagation.Signing.UnitTests.Infrastructure;

namespace ContextR.Propagation.Signing.UnitTests;

public sealed class SigningContextPropagatorTests
{
    private readonly byte[] _testKey = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void Inject_AddsSignatureHeader()
    {
        var propagator = TestHelper.BuildPropagator(_testKey);

        var headers = TestHelper.InjectToHeaders(propagator, new TestContext
        {
            TenantId = "acme",
            Region = "us-east-1"
        });

        Assert.True(headers.ContainsKey("X-Context-Signature"));
        Assert.Contains(".", headers["X-Context-Signature"]);
    }

    [Fact]
    public void RoundTrip_ValidSignature_ExtractsContext()
    {
        var propagator = TestHelper.BuildPropagator(_testKey);

        var original = new TestContext { TenantId = "acme", Region = "us-east-1" };
        var headers = TestHelper.InjectToHeaders(propagator, original);
        var extracted = TestHelper.ExtractFromHeaders(propagator, headers);

        Assert.NotNull(extracted);
        Assert.Equal("acme", extracted.TenantId);
        Assert.Equal("us-east-1", extracted.Region);
    }

    [Fact]
    public void Extract_TamperedValue_ReturnsNull_WithSkipContextHandler()
    {
        var propagator = TestHelper.BuildPropagator(_testKey,
            onFailure: _ => PropagationFailureAction.SkipContext);

        var headers = TestHelper.InjectToHeaders(propagator, new TestContext
        {
            TenantId = "acme",
            Region = "us-east-1"
        });

        headers["X-Tenant-Id"] = "evil-tenant";

        var extracted = TestHelper.ExtractFromHeaders(propagator, headers);
        Assert.Null(extracted);
    }

    [Fact]
    public void Extract_TamperedValue_Throws_WithDefaultHandler()
    {
        var propagator = TestHelper.BuildPropagator(_testKey);

        var headers = TestHelper.InjectToHeaders(propagator, new TestContext
        {
            TenantId = "acme",
            Region = "us-east-1"
        });

        headers["X-Tenant-Id"] = "evil-tenant";

        Assert.Throws<InvalidOperationException>(() =>
            TestHelper.ExtractFromHeaders(propagator, headers));
    }

    [Fact]
    public void Extract_AddedHeader_ReturnsNull_WithSkipContextHandler()
    {
        var propagator = TestHelper.BuildPropagator(_testKey,
            onFailure: _ => PropagationFailureAction.SkipContext);

        var original = new TestContext { TenantId = "acme" };
        var headers = TestHelper.InjectToHeaders(propagator, original);

        headers["X-Region"] = "injected-region";

        var extracted = TestHelper.ExtractFromHeaders(propagator, headers);
        Assert.Null(extracted);
    }

    [Fact]
    public void Extract_RemovedHeader_SignatureFails()
    {
        var propagator = TestHelper.BuildPropagator(_testKey,
            onFailure: _ => PropagationFailureAction.SkipContext);

        var headers = TestHelper.InjectToHeaders(propagator, new TestContext
        {
            TenantId = "acme",
            Region = "us-east-1"
        });

        headers.Remove("X-Region");

        var extracted = TestHelper.ExtractFromHeaders(propagator, headers);
        Assert.Null(extracted);
    }

    [Fact]
    public void Extract_MissingSignature_ReturnsNull_WithSkipContextHandler()
    {
        var propagator = TestHelper.BuildPropagator(_testKey,
            onFailure: _ => PropagationFailureAction.SkipContext);

        var headers = new Dictionary<string, string>
        {
            ["X-Tenant-Id"] = "acme",
            ["X-Region"] = "us-east-1"
        };

        var extracted = TestHelper.ExtractFromHeaders(propagator, headers);
        Assert.Null(extracted);
    }

    [Fact]
    public void Extract_MalformedSignature_ReturnsNull_WithSkipContextHandler()
    {
        var propagator = TestHelper.BuildPropagator(_testKey,
            onFailure: _ => PropagationFailureAction.SkipContext);

        var headers = new Dictionary<string, string>
        {
            ["X-Tenant-Id"] = "acme",
            ["X-Region"] = "us-east-1",
            ["X-Context-Signature"] = "not-a-valid-signature"
        };

        var extracted = TestHelper.ExtractFromHeaders(propagator, headers);
        Assert.Null(extracted);
    }

    [Fact]
    public void Inject_EmptyContext_DoesNotAddSignature()
    {
        var propagator = TestHelper.BuildPropagator(_testKey);

        var headers = TestHelper.InjectToHeaders(propagator, new TestContext());

        Assert.False(headers.ContainsKey("X-Context-Signature"));
    }

    [Fact]
    public void CustomSignatureHeader_IsUsed()
    {
        var propagator = TestHelper.BuildPropagator(_testKey, configureOptions: o =>
        {
            o.Key = _testKey;
            o.SignatureHeader = "X-Custom-Sig";
        });

        var headers = TestHelper.InjectToHeaders(propagator, new TestContext
        {
            TenantId = "acme"
        });

        Assert.True(headers.ContainsKey("X-Custom-Sig"));
        Assert.False(headers.ContainsKey("X-Context-Signature"));
    }
}
