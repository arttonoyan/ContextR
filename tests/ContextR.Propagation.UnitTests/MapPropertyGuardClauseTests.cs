using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Propagation.UnitTests;

public sealed class MapPropertyGuardClauseTests
{
    [Fact]
    public void MapProperty_Throws_WhenPropertyExpressionIsNull()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<TestContext>(reg => reg
                    .MapProperty((Expression<Func<TestContext, string?>>)null!, "X-Key"));
            });
        });
    }

    [Fact]
    public void MapProperty_Throws_WhenKeyIsNull()
    {
        var services = new ServiceCollection();

        Assert.ThrowsAny<ArgumentException>(() =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<TestContext>(reg => reg
                    .MapProperty(c => c.TenantId, null!));
            });
        });
    }

    [Fact]
    public void MapProperty_Throws_WhenKeyIsEmpty()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<TestContext>(reg => reg
                    .MapProperty(c => c.TenantId, ""));
            });
        });
    }

    [Fact]
    public void MapProperty_Throws_WhenKeyIsWhitespace()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
        {
            services.AddContextR(builder =>
            {
                builder.Add<TestContext>(reg => reg
                    .MapProperty(c => c.TenantId, "   "));
            });
        });
    }

    public class TestContext
    {
        public string? TenantId { get; set; }
    }
}
