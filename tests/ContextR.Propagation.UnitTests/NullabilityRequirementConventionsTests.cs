using System.Linq.Expressions;
using ContextR.Propagation.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Propagation.UnitTests;

public sealed class NullabilityRequirementConventionsTests
{
    [Fact]
    public void ResolveRequirement_Throws_WhenExpressionIsNotMemberAccess()
    {
        Expression<Func<ConventionContext, string>> expression = c => c.ToString()!;

        Assert.Throws<ArgumentException>(() =>
            NullabilityRequirementConventions.ResolveRequirement(expression));
    }

    [Fact]
    public void ResolveRequirement_Throws_WhenMemberIsNotProperty()
    {
        Expression<Func<FieldConventionContext, string>> expression = c => c.Value;

        Assert.Throws<ArgumentException>(() =>
            NullabilityRequirementConventions.ResolveRequirement(expression));
    }

    [Fact]
    public void ResolveRequirement_ReturnsExpected_ForNullableAndNonNullableValueTypes()
    {
        var required = NullabilityRequirementConventions.ResolveRequirement<ValueTypeContext, int>(c => c.RequiredCount);
        var optional = NullabilityRequirementConventions.ResolveRequirement<ValueTypeContext, int?>(c => c.OptionalCount);

        Assert.Equal(PropertyRequirement.Required, required);
        Assert.Equal(PropertyRequirement.Optional, optional);
    }

    [Fact]
    public void GetOrAddOptions_AndIsEnabled_WorkAsExpected()
    {
        var services = new ServiceCollection();

        var first = NullabilityRequirementConventions.GetOrAddOptions<ConventionContext>(services);
        var second = NullabilityRequirementConventions.GetOrAddOptions<ConventionContext>(services);
        first.Enabled = false;

        Assert.Same(first, second);
        Assert.False(NullabilityRequirementConventions.IsEnabled<ConventionContext>(services));
        Assert.True(NullabilityRequirementConventions.IsEnabled<ValueTypeContext>(services));
    }

    private sealed class ConventionContext
    {
        public required string TenantId { get; set; }
    }

    private sealed class ValueTypeContext
    {
        public int RequiredCount { get; set; }
        public int? OptionalCount { get; set; }
    }

    private sealed class FieldConventionContext
    {
        public string Value = string.Empty;
    }
}
