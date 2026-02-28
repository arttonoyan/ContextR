namespace ContextR.Propagation.Token.UnitTests;

public sealed class ContextPayloadTokenReferenceTests
{
    [Fact]
    public void Constructor_SetsTokenAndVersion()
    {
        var reference = new ContextPayloadTokenReference("token-1", "v2");

        Assert.Equal("token-1", reference.Token);
        Assert.Equal("v2", reference.Version);
    }

    [Fact]
    public void Constructor_DefaultVersion_IsNull()
    {
        var reference = new ContextPayloadTokenReference("token-1");

        Assert.Equal("token-1", reference.Token);
        Assert.Null(reference.Version);
    }

    [Fact]
    public void ValueEquality_WorksForSameValues()
    {
        var left = new ContextPayloadTokenReference("token-1", "v1");
        var right = new ContextPayloadTokenReference("token-1", "v1");

        Assert.Equal(left, right);
        Assert.True(left == right);
    }

    [Fact]
    public void ValueEquality_DiffersForDifferentValues()
    {
        var left = new ContextPayloadTokenReference("token-1", "v1");
        var right = new ContextPayloadTokenReference("token-2", "v1");

        Assert.NotEqual(left, right);
        Assert.True(left != right);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        var original = new ContextPayloadTokenReference("token-1", "v1");
        var updated = original with { Version = "v2" };

        Assert.Equal("token-1", updated.Token);
        Assert.Equal("v2", updated.Version);
        Assert.Equal("v1", original.Version);
    }
}
