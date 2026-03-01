using System.Linq.Expressions;
using ContextR.Propagation.Internal;

namespace ContextR.Propagation.UnitTests;

public sealed class PropertyMappingValidationTests
{
    [Fact]
    public void Create_Throws_WhenExpressionIsNotMemberAccess()
    {
        Expression<Func<TestContext, string>> expr = c => c.ToString()!;

        var ex = Assert.Throws<ArgumentException>(() =>
            PropertyMapping.Create(expr, "X-Key"));

        Assert.Contains("property access", ex.Message);
    }

    [Fact]
    public void Create_Throws_WhenExpressionReferencesField()
    {
        Expression<Func<FieldContext, string>> expr = c => c.FieldValue;

        var ex = Assert.Throws<ArgumentException>(() =>
            PropertyMapping.Create(expr, "X-Key"));

        Assert.Contains("property", ex.Message);
    }

    [Fact]
    public void Create_Throws_WhenPropertyIsReadOnly()
    {
        Expression<Func<ReadOnlyContext, string>> expr = c => c.ReadOnly;

        var ex = Assert.Throws<ArgumentException>(() =>
            PropertyMapping.Create(expr, "X-Key"));

        Assert.Contains("writable", ex.Message);
    }

    [Fact]
    public void TrySetValue_ReturnsFalse_WhenIParsableParseFails()
    {
        var mapping = PropertyMapping.Create<IntContext, int>(c => c.Count, "X-Count");

        var context = new IntContext();
        var result = mapping.TrySetValue(context, "not-a-number");

        Assert.False(result);
    }

    [Fact]
    public void TrySetValue_ReturnsTrue_WhenConvertChangeTypeSucceeds()
    {
        var mapping = PropertyMapping.Create<DoubleContext, double>(c => c.Value, "X-Value");

        var context = new DoubleContext();
        var result = mapping.TrySetValue(context, "3.14");

        Assert.True(result);
        Assert.Equal(3.14, context.Value, precision: 2);
    }

    [Fact]
    public void GetValue_ReturnsToString_ForValueTypes()
    {
        var mapping = PropertyMapping.Create<IntContext, int>(c => c.Count, "X-Count");

        var context = new IntContext { Count = 42 };
        var value = mapping.GetValues(context).Single().Value;

        Assert.Equal("42", value);
    }

    [Fact]
    public void GetValue_ReturnsNull_WhenReferencePropertyIsNull()
    {
        var mapping = PropertyMapping.Create<TestContext, string?>(c => c.TenantId, "X-Tenant-Id");

        var context = new TestContext();
        var values = mapping.GetValues(context);
        Assert.Empty(values);
    }

    [Fact]
    public void TrySetValue_ReturnsTrue_ForStringProperty()
    {
        var mapping = PropertyMapping.Create<TestContext, string?>(c => c.TenantId, "X-Tenant-Id");

        var context = new TestContext();
        var result = mapping.TrySetValue(context, "hello");

        Assert.True(result);
        Assert.Equal("hello", context.TenantId);
    }

    [Fact]
    public void TrySetValue_UsesConvertChangeType_ForTypeWithoutIParsableTryParse()
    {
        var mapping = PropertyMapping.Create<CharContext, char>(c => c.Letter, "X-Letter");

        var context = new CharContext();
        var result = mapping.TrySetValue(context, "a");

        Assert.True(result);
        Assert.Equal('a', context.Letter);
    }

    [Fact]
    public void TrySetValue_ReturnsFalse_WhenConvertChangeTypeThrows()
    {
        var mapping = PropertyMapping.Create<CharContext, char>(c => c.Letter, "X-Letter");

        var context = new CharContext();
        var result = mapping.TrySetValue(context, "multi-char-string");

        Assert.False(result);
    }

    [Fact]
    public void TrySetValue_ReturnsFalse_ForArrayProperty()
    {
        var mapping = PropertyMapping.Create<ArrayContext, string[]>(c => c.Tags, "X-Tags");

        var context = new ArrayContext();
        var result = mapping.TrySetValue(context, "a,b,c");

        Assert.False(result);
        Assert.Empty(context.Tags);
    }

    [Fact]
    public void TrySetValue_ReturnsFalse_ForListProperty()
    {
        var mapping = PropertyMapping.Create<ListContext, List<string>>(c => c.Tags, "X-Tags");

        var context = new ListContext();
        var result = mapping.TrySetValue(context, "a,b,c");

        Assert.False(result);
        Assert.Empty(context.Tags);
    }

    [Fact]
    public void TrySetValue_ReturnsFalse_ForCustomClassProperty()
    {
        var mapping = PropertyMapping.Create<CustomClassContext, UserInfo?>(c => c.User, "X-User");

        var context = new CustomClassContext();
        var result = mapping.TrySetValue(context, "alice");

        Assert.False(result);
        Assert.Null(context.User);
    }

    [Fact]
    public void GetValue_UsesToString_ForCustomClassProperty()
    {
        var mapping = PropertyMapping.Create<CustomClassContext, UserInfo?>(c => c.User, "X-User");
        var context = new CustomClassContext
        {
            User = new UserInfo { Name = "alice" }
        };

        var value = mapping.GetValues(context).Single().Value;

        Assert.Equal("alice", value);
    }

    [Fact]
    public void Key_ReturnsConfiguredKey()
    {
        var mapping = PropertyMapping.Create<TestContext, string?>(c => c.TenantId, "X-Custom-Key");

        Assert.Equal("X-Custom-Key", mapping.Key);
    }

    public class TestContext
    {
        public string? TenantId { get; set; }
    }

    public class IntContext
    {
        public int Count { get; set; }
    }

    public class DoubleContext
    {
        public double Value { get; set; }
    }

    public class CharContext
    {
        public char Letter { get; set; }
    }

    public class ArrayContext
    {
        public string[] Tags { get; set; } = [];
    }

    public class ListContext
    {
        public List<string> Tags { get; set; } = [];
    }

    public class CustomClassContext
    {
        public UserInfo? User { get; set; }
    }

    public class UserInfo
    {
        public string Name { get; set; } = string.Empty;

        public override string ToString() => Name;
    }

    public class FieldContext
    {
#pragma warning disable CS0649
        public string FieldValue;
#pragma warning restore CS0649
    }

    public class ReadOnlyContext
    {
        public string ReadOnly { get; } = "fixed";
    }
}
