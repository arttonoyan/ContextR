using System.Text;
using ContextR.Propagation.Signing.Internal;

namespace ContextR.Propagation.Signing.UnitTests;

public sealed class CanonicalSigningInputTests
{
    [Fact]
    public void Build_SortsByKeyOrdinal()
    {
        var pairs = new Dictionary<string, string>
        {
            ["X-Region"] = "us-east-1",
            ["X-Tenant-Id"] = "acme",
            ["X-Correlation"] = "abc-123"
        };

        var result = Encoding.UTF8.GetString(CanonicalSigningInput.Build(pairs));

        Assert.Equal("X-Correlation=abc-123\nX-Region=us-east-1\nX-Tenant-Id=acme\n", result);
    }

    [Fact]
    public void Build_EmptyPairs_ReturnsEmptyBytes()
    {
        var result = CanonicalSigningInput.Build([]);
        Assert.Empty(result);
    }

    [Fact]
    public void Build_SinglePair_HasTrailingNewline()
    {
        var pairs = new Dictionary<string, string> { ["X-Key"] = "value" };
        var result = Encoding.UTF8.GetString(CanonicalSigningInput.Build(pairs));

        Assert.Equal("X-Key=value\n", result);
    }

    [Fact]
    public void Build_OrdinalSort_UppercaseBeforeLowercase()
    {
        var pairs = new Dictionary<string, string>
        {
            ["b-key"] = "lower",
            ["A-key"] = "upper"
        };

        var result = Encoding.UTF8.GetString(CanonicalSigningInput.Build(pairs));

        Assert.Equal("A-key=upper\nb-key=lower\n", result);
    }

    [Fact]
    public void Build_ValuesWithSpecialCharacters_PreservedLiterally()
    {
        var pairs = new Dictionary<string, string>
        {
            ["X-Data"] = "value=with=equals\nnewline"
        };

        var result = Encoding.UTF8.GetString(CanonicalSigningInput.Build(pairs));

        Assert.Equal("X-Data=value=with=equals\nnewline\n", result);
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var pairs = new Dictionary<string, string>
        {
            ["X-B"] = "2",
            ["X-A"] = "1"
        };

        var result1 = CanonicalSigningInput.Build(pairs);
        var result2 = CanonicalSigningInput.Build(pairs);

        Assert.Equal(result1, result2);
    }
}
