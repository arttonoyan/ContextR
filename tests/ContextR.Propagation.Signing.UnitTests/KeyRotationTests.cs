using System.Security.Cryptography;
using ContextR.Propagation.Signing.UnitTests.Infrastructure;

namespace ContextR.Propagation.Signing.UnitTests;

public sealed class KeyRotationTests
{
    [Fact]
    public void SignedWithV1_VerifiedWithV1_WhenV2IsCurrent_InlineKeys()
    {
        var keyV1 = RandomNumberGenerator.GetBytes(32);
        var keyV2 = RandomNumberGenerator.GetBytes(32);

        var signerV1 = TestHelper.BuildPropagator(keyV1);

        var original = new TestContext { TenantId = "acme", Region = "eu-west-1" };
        var headers = TestHelper.InjectToHeaders(signerV1, original);

        var verifierV2 = TestHelper.BuildPropagator(keyV2, configureOptions: o =>
        {
            o.AddKey(1, keyV1);
            o.AddKey(2, keyV2);
            o.CurrentKeyVersion = 2;
        });

        var extracted = TestHelper.ExtractFromHeaders(verifierV2, headers);

        Assert.NotNull(extracted);
        Assert.Equal("acme", extracted.TenantId);
        Assert.Equal("eu-west-1", extracted.Region);
    }

    [Fact]
    public void SignedWithV2_VerifiedWithV2_InlineKeys()
    {
        var keyV1 = RandomNumberGenerator.GetBytes(32);
        var keyV2 = RandomNumberGenerator.GetBytes(32);

        var propagator = TestHelper.BuildPropagator(keyV2, configureOptions: o =>
        {
            o.AddKey(1, keyV1);
            o.AddKey(2, keyV2);
            o.CurrentKeyVersion = 2;
        });

        var original = new TestContext { TenantId = "acme", Region = "eu-west-1" };
        var headers = TestHelper.InjectToHeaders(propagator, original);

        Assert.Contains(".2", headers["X-Context-Signature"]);

        var extracted = TestHelper.ExtractFromHeaders(propagator, headers);
        Assert.NotNull(extracted);
        Assert.Equal("acme", extracted.TenantId);
    }

    [Fact]
    public void SignedWithV1_FailsVerification_WhenV1KeyRevoked_InlineKeys()
    {
        var keyV1 = RandomNumberGenerator.GetBytes(32);
        var keyV2 = RandomNumberGenerator.GetBytes(32);

        var signerV1 = TestHelper.BuildPropagator(keyV1,
            onFailure: _ => PropagationFailureAction.SkipContext);

        var headers = TestHelper.InjectToHeaders(signerV1, new TestContext
        {
            TenantId = "acme",
            Region = "eu-west-1"
        });

        var verifierV2Only = TestHelper.BuildPropagator(keyV2, configureOptions: o =>
        {
            o.AddKey(2, keyV2);
            o.CurrentKeyVersion = 2;
        },
            onFailure: _ => PropagationFailureAction.SkipContext);

        var extracted = TestHelper.ExtractFromHeaders(verifierV2Only, headers);
        Assert.Null(extracted);
    }

    [Fact]
    public void SignedWithV1_VerifiedWithV1_WhenV2IsCurrent_WithProvider()
    {
        var keyV1 = RandomNumberGenerator.GetBytes(32);
        var keyV2 = RandomNumberGenerator.GetBytes(32);

        var providerV1 = new StaticSigningKeyProvider("k1", 1, keyV1);
        var signerV1 = TestHelper.BuildPropagatorWithProvider(providerV1, o => o.KeyId = "k1");

        var original = new TestContext { TenantId = "acme", Region = "eu-west-1" };
        var headers = TestHelper.InjectToHeaders(signerV1, original);

        var providerV2 = new StaticSigningKeyProvider("k1", 2, keyV2);
        providerV2.AddKey("k1", 1, keyV1);
        var verifierV2 = TestHelper.BuildPropagatorWithProvider(providerV2, o => o.KeyId = "k1");

        var extracted = TestHelper.ExtractFromHeaders(verifierV2, headers);

        Assert.NotNull(extracted);
        Assert.Equal("acme", extracted.TenantId);
        Assert.Equal("eu-west-1", extracted.Region);
    }
}
