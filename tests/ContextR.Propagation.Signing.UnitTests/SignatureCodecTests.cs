using ContextR.Propagation.Signing.Internal;

namespace ContextR.Propagation.Signing.UnitTests;

public sealed class SignatureCodecTests
{
    [Fact]
    public void RoundTrip_PreservesHmacAndVersion()
    {
        var originalHmac = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        var encoded = SignatureCodec.Encode(originalHmac, 42);

        Assert.True(SignatureCodec.TryDecode(encoded, out var decodedHmac, out var version));
        Assert.Equal(originalHmac, decodedHmac);
        Assert.Equal(42, version);
    }

    [Fact]
    public void Encode_ProducesHeaderSafeValue()
    {
        var hmac = new byte[32];
        Random.Shared.NextBytes(hmac);
        var encoded = SignatureCodec.Encode(hmac, 1);

        Assert.DoesNotContain("+", encoded);
        Assert.DoesNotContain("/", encoded);
        Assert.DoesNotContain("=", encoded.AsSpan(0, encoded.LastIndexOf('.')));
        Assert.Contains(".", encoded);
    }

    [Fact]
    public void TryDecode_MissingSeparator_ReturnsFalse()
    {
        Assert.False(SignatureCodec.TryDecode("noseparator", out _, out _));
    }

    [Fact]
    public void TryDecode_NonNumericVersion_ReturnsFalse()
    {
        Assert.False(SignatureCodec.TryDecode("AQIDBA.abc", out _, out _));
    }

    [Fact]
    public void TryDecode_EmptyHmacPart_ReturnsFalse()
    {
        Assert.False(SignatureCodec.TryDecode(".5", out _, out _));
    }

    [Fact]
    public void TryDecode_EmptyVersionPart_ReturnsFalse()
    {
        Assert.False(SignatureCodec.TryDecode("AQIDBA.", out _, out _));
    }

    [Fact]
    public void TryDecode_InvalidBase64_ReturnsFalse()
    {
        Assert.False(SignatureCodec.TryDecode("!!!invalid!!!.1", out _, out _));
    }
}
