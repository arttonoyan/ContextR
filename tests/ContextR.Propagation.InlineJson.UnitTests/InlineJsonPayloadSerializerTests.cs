using System.Text.Json;

namespace ContextR.Propagation.InlineJson.UnitTests;

public sealed class InlineJsonPayloadSerializerTests
{
    private readonly InlineJsonPayloadSerializer<TestContext> _sut = new();

    [Theory]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(Guid), false)]
    [InlineData(typeof(int?), false)]
    [InlineData(typeof(DateTime), false)]
    [InlineData(typeof(DateTimeOffset), false)]
    [InlineData(typeof(TimeSpan), false)]
    [InlineData(typeof(Status), false)]
    [InlineData(typeof(CustomConvertible), false)]
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
    public void TryDeserialize_ReturnsFalse_ForUnsupportedType()
    {
        var ok = _sut.TryDeserialize("{\"a\":1}", typeof(Stream), out var parsed);

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

    private sealed class CustomConvertible : IConvertible
    {
        public TypeCode GetTypeCode() => TypeCode.Object;
        public bool ToBoolean(IFormatProvider? provider) => false;
        public byte ToByte(IFormatProvider? provider) => 0;
        public char ToChar(IFormatProvider? provider) => '\0';
        public DateTime ToDateTime(IFormatProvider? provider) => default;
        public decimal ToDecimal(IFormatProvider? provider) => 0m;
        public double ToDouble(IFormatProvider? provider) => 0d;
        public short ToInt16(IFormatProvider? provider) => 0;
        public int ToInt32(IFormatProvider? provider) => 0;
        public long ToInt64(IFormatProvider? provider) => 0L;
        public sbyte ToSByte(IFormatProvider? provider) => 0;
        public float ToSingle(IFormatProvider? provider) => 0f;
        public string ToString(IFormatProvider? provider) => string.Empty;
        public object ToType(Type conversionType, IFormatProvider? provider) => string.Empty;
        public ushort ToUInt16(IFormatProvider? provider) => 0;
        public uint ToUInt32(IFormatProvider? provider) => 0U;
        public ulong ToUInt64(IFormatProvider? provider) => 0UL;
    }
}
