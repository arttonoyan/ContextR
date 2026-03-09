namespace ContextR.Propagation.Chunking.UnitTests;

/// <summary>
/// Verifies that TryReassemble rejects chunk counts exceeding the configured upper bound,
/// preventing resource exhaustion from untrusted carrier values.
/// </summary>
public sealed class ChunkingSecurityTests
{
    [Fact]
    public void TryReassemble_RejectsChunkCountAboveLimit()
    {
        var strategy = new DefaultPayloadChunkingStrategy<TestContext>();
        const int maliciousChunkCount = 50_000;

        var ok = strategy.TryReassemble(
            "X-Payload",
            (object?)null,
            (_, key) =>
            {
                if (key == "X-Payload__chunks")
                    return maliciousChunkCount.ToString();
                return "A";
            },
            out var payload);

        Assert.False(ok);
        Assert.Null(payload);
    }

    [Fact]
    public void TryReassemble_RejectsExtremeChunkCount()
    {
        var strategy = new DefaultPayloadChunkingStrategy<TestContext>();
        const int maliciousChunkCount = 100_000;

        var ok = strategy.TryReassemble(
            "X-Payload",
            (object?)null,
            (_, key) =>
            {
                if (key == "X-Payload__chunks")
                    return maliciousChunkCount.ToString();
                return new string('X', 1024);
            },
            out var payload);

        Assert.False(ok);
        Assert.Null(payload);
    }

    [Fact]
    public void TryReassemble_AcceptsChunkCountWithinLimit()
    {
        var strategy = new DefaultPayloadChunkingStrategy<TestContext>();
        const int validChunkCount = 5;

        var ok = strategy.TryReassemble(
            "X-Payload",
            (object?)null,
            (_, key) =>
            {
                if (key == "X-Payload__chunks")
                    return validChunkCount.ToString();
                return "A";
            },
            out var payload);

        Assert.True(ok);
        Assert.Equal(new string('A', validChunkCount), payload);
    }

    [Fact]
    public void TryReassemble_AcceptsExactlyMaxChunkCount()
    {
        var strategy = new DefaultPayloadChunkingStrategy<TestContext>();

        var ok = strategy.TryReassemble(
            "X-Payload",
            (object?)null,
            (_, key) =>
            {
                if (key == "X-Payload__chunks")
                    return DefaultPayloadChunkingStrategy<TestContext>.MaxChunkCount.ToString();
                return "A";
            },
            out var payload);

        Assert.True(ok);
        Assert.NotNull(payload);
    }

    private sealed class TestContext;
}
