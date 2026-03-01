namespace ContextR.Propagation.Chunking.UnitTests;

public sealed class DefaultPayloadChunkingStrategyTests
{
    [Fact]
    public void Chunk_SplitsPayloadAndProducesCountAndParts()
    {
        var strategy = new DefaultPayloadChunkingStrategy<TestContext>();
        var parts = strategy.Chunk("X-Payload", new string('a', 20), maxPayloadBytes: 8).ToArray();

        Assert.Equal("3", parts.Single(p => p.Key == "X-Payload__chunks").Value);
        Assert.Equal(4, parts.Length);
        Assert.Contains(parts, p => p.Key == "X-Payload__chunk_0");
        Assert.Contains(parts, p => p.Key == "X-Payload__chunk_1");
        Assert.Contains(parts, p => p.Key == "X-Payload__chunk_2");
    }

    [Fact]
    public void Chunk_Throws_WhenMaxPayloadBytesIsZeroOrLess()
    {
        var strategy = new DefaultPayloadChunkingStrategy<TestContext>();
        Assert.Throws<InvalidOperationException>(() =>
            strategy.Chunk("X-Payload", "data", maxPayloadBytes: 0).ToArray());
    }

    [Fact]
    public void TryReassemble_ReturnsFalse_WhenCountHeaderMissing()
    {
        var strategy = new DefaultPayloadChunkingStrategy<TestContext>();
        var carrier = new Dictionary<string, string>();

        var ok = strategy.TryReassemble("X-Payload", carrier, static (c, k) => c.TryGetValue(k, out var v) ? v : null, out var payload);
        Assert.False(ok);
        Assert.Null(payload);
    }

    [Fact]
    public void TryReassemble_ReturnsFalse_WhenAnyChunkIsMissing()
    {
        var strategy = new DefaultPayloadChunkingStrategy<TestContext>();
        var carrier = new Dictionary<string, string>
        {
            ["X-Payload__chunks"] = "2",
            ["X-Payload__chunk_0"] = "hello"
        };

        var ok = strategy.TryReassemble("X-Payload", carrier, static (c, k) => c.TryGetValue(k, out var v) ? v : null, out var payload);
        Assert.False(ok);
        Assert.Null(payload);
    }

    [Fact]
    public void TryReassemble_ReconstructsOriginalPayload()
    {
        var strategy = new DefaultPayloadChunkingStrategy<TestContext>();
        var source = "payload-value";
        var chunked = strategy.Chunk("X-Payload", source, maxPayloadBytes: 4).ToDictionary(x => x.Key, x => x.Value);

        var ok = strategy.TryReassemble("X-Payload", chunked, static (c, k) => c.TryGetValue(k, out var v) ? v : null, out var payload);
        Assert.True(ok);
        Assert.Equal(source, payload);
    }

    [Fact]
    public void Chunk_HandlesMultiByteUtf8WithoutBreakingRunes()
    {
        var strategy = new DefaultPayloadChunkingStrategy<TestContext>();
        var source = "ab😀cd😀ef";
        var chunked = strategy.Chunk("X-Payload", source, maxPayloadBytes: 5).ToDictionary(x => x.Key, x => x.Value);

        var ok = strategy.TryReassemble("X-Payload", chunked, static (c, k) => c.TryGetValue(k, out var v) ? v : null, out var payload);
        Assert.True(ok);
        Assert.Equal(source, payload);
    }

    private sealed class TestContext;
}
