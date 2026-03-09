using ContextR.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.UnitTests;

public sealed class InternalTypeTests
{
    [Fact]
    public void ContextKey_Domain_PropertyIsAccessible()
    {
        var key = new ContextKey("my-domain", typeof(string));

        Assert.Equal("my-domain", key.Domain);
        Assert.Equal(typeof(string), key.ContextType);
    }

    [Fact]
    public void ContextKey_NullDomain_IsValid()
    {
        var key = new ContextKey(null, typeof(int));

        Assert.Null(key.Domain);
        Assert.Equal(typeof(int), key.ContextType);
    }

    [Fact]
    public void ContextKey_Equality_MatchesOnBothComponents()
    {
        var key1 = new ContextKey("domain-a", typeof(string));
        var key2 = new ContextKey("domain-a", typeof(string));
        var key3 = new ContextKey("domain-b", typeof(string));
        var key4 = new ContextKey("domain-a", typeof(int));

        Assert.Equal(key1, key2);
        Assert.NotEqual(key1, key3);
        Assert.NotEqual(key1, key4);
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    [Fact]
    public void ContextKey_WorksAsDictionaryKey()
    {
        var dict = new Dictionary<ContextKey, string>
        {
            [new ContextKey(null, typeof(string))] = "default-string",
            [new ContextKey("web", typeof(string))] = "web-string",
            [new ContextKey(null, typeof(int))] = "default-int",
        };

        Assert.Equal("default-string", dict[new ContextKey(null, typeof(string))]);
        Assert.Equal("web-string", dict[new ContextKey("web", typeof(string))]);
        Assert.Equal("default-int", dict[new ContextKey(null, typeof(int))]);
    }

    [Fact]
    public void ContextSnapshot_Constructor_DefensiveCopiesValues()
    {
        var original = new Dictionary<ContextKey, object>
        {
            [new ContextKey(null, typeof(UserContext))] = new UserContext("original")
        };

        var snapshot = new ContextSnapshot(original, null);
        original[new ContextKey(null, typeof(UserContext))] = new UserContext("mutated");

        Assert.Equal("original", snapshot.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void ContextSnapshot_GetContext_ReturnsNull_WhenTypeMismatch()
    {
        var values = new Dictionary<ContextKey, object>
        {
            [new ContextKey(null, typeof(UserContext))] = "not-a-user-context"
        };

        var snapshot = new ContextSnapshot(values, null);

        Assert.Null(snapshot.GetContext<UserContext>());
    }

    [Fact]
    public void ContextSnapshot_GetContextDomain_ReturnsNull_WhenTypeMismatch()
    {
        var values = new Dictionary<ContextKey, object>
        {
            [new ContextKey("domain", typeof(UserContext))] = "not-a-user-context"
        };

        var snapshot = new ContextSnapshot(values, null);

        Assert.Null(snapshot.GetContext<UserContext>("domain"));
    }

    [Fact]
    public void CreateSnapshot_WithNonMutableAccessor_DefaultDomainIsNull()
    {
        var accessor = new StubContextAccessor();

        var snapshot = accessor.CreateSnapshot();

        Assert.Null(snapshot.GetContext<UserContext>());
    }

    [Fact]
    public void CreateSnapshotGeneric_WithNonMutableAccessor_DefaultDomainIsNull()
    {
        var accessor = new StubContextAccessor();

        var snapshot = accessor.CreateSnapshot(new UserContext("test"));

        Assert.Equal("test", snapshot.GetContext<UserContext>()?.UserId);
    }

    [Fact]
    public void CreateSnapshotDomain_WithNonMutableAccessor_DefaultDomainIsNull()
    {
        var accessor = new StubContextAccessor();

        var snapshot = accessor.CreateSnapshot("my-domain", new UserContext("test"));

        Assert.Equal("test", snapshot.GetContext<UserContext>("my-domain")?.UserId);
        Assert.Null(snapshot.GetContext<UserContext>());
    }

    private sealed record UserContext(string UserId);

    private sealed class StubContextAccessor : IContextAccessor
    {
        public object? GetContext(Type contextType) => null;
        public object? GetContext(string domain, Type contextType) => null;

        public IContextSnapshot CreateSnapshot() =>
            new ContextSnapshot(new Dictionary<ContextKey, object>(), null);

        public IContextSnapshot CreateSnapshot(Type contextType, object context) =>
            new ContextSnapshot(new Dictionary<ContextKey, object>
            {
                [new ContextKey(null, contextType)] = context
            }, null);

        public IContextSnapshot CreateSnapshot(string domain, Type contextType, object context) =>
            new ContextSnapshot(new Dictionary<ContextKey, object>
            {
                [new ContextKey(domain, contextType)] = context
            }, null);
    }
}
