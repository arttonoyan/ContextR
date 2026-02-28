using System.Text.Json;

namespace ContextR.Propagation.InlineJson.UnitTests;

public sealed class InlineJsonPayloadSerializerTests
{
    private readonly InlineJsonPayloadSerializer<TestContext> _sut = new();

    [Theory]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(Guid), false)]
    [InlineData(typeof(DateTime), false)]
    [InlineData(typeof(Status), false)]
    [InlineData(typeof(List<string>), true)]
    [InlineData(typeof(string[]), true)]
    [InlineData(typeof(UserPayload), true)]
    public void CanHandle_ReturnsExpectedValue(Type propertyType, bool expected)
    {
        Assert.Equal(expected, _sut.CanHandle(propertyType));
    }

    [Fact]
    public void Serialize_And_TryDeserialize_RoundTripsList()
    {
        var payload = new List<string> { "a", "b" };
        var serialized = _sut.Serialize(payload, typeof(List<string>));

        var ok = _sut.TryDeserialize(serialized, typeof(List<string>), out var parsed);

        Assert.True(ok);
        Assert.NotNull(parsed);
        Assert.Equal(payload, Assert.IsType<List<string>>(parsed));
    }

    [Fact]
    public void Serialize_And_TryDeserialize_RoundTripsCustomObject()
    {
        var payload = new UserPayload { Name = "alice", Age = 30 };
        var serialized = _sut.Serialize(payload, typeof(UserPayload));

        var ok = _sut.TryDeserialize(serialized, typeof(UserPayload), out var parsed);

        Assert.True(ok);
        var typed = Assert.IsType<UserPayload>(parsed);
        Assert.Equal("alice", typed.Name);
        Assert.Equal(30, typed.Age);
    }

    [Fact]
    public void TryDeserialize_ReturnsFalse_ForInvalidJson()
    {
        var ok = _sut.TryDeserialize("{ this is invalid json", typeof(UserPayload), out var parsed);

        Assert.False(ok);
        Assert.Null(parsed);
    }

    [Fact]
    public void Constructor_UsesProvidedJsonOptions()
    {
        var serializer = new InlineJsonPayloadSerializer<TestContext>(
            new JsonSerializerOptions { PropertyNamingPolicy = null });

        var serialized = serializer.Serialize(new UserPayload { Name = "alice", Age = 20 }, typeof(UserPayload));

        Assert.Contains("\"Name\"", serialized);
        Assert.DoesNotContain("\"name\"", serialized);
    }

    private sealed class TestContext;

    private sealed class UserPayload
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private enum Status
    {
        Unknown = 0,
        Active = 1
    }
}
