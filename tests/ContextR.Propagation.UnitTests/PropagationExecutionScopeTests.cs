using System.Collections.Concurrent;

namespace ContextR.Propagation.UnitTests;

public sealed class PropagationExecutionScopeTests
{
    [Fact]
    public void CurrentDomain_DefaultsToNull()
    {
        var scope = new AsyncLocalPropagationExecutionScope();
        using var _ = scope.BeginDomainScope(null);

        Assert.Null(scope.CurrentDomain);
    }

    [Fact]
    public void BeginDomainScope_SetsDomain_AndRestoresOnDispose()
    {
        var scope = new AsyncLocalPropagationExecutionScope();
        using var _ = scope.BeginDomainScope(null);

        using (scope.BeginDomainScope("orders"))
        {
            Assert.Equal("orders", scope.CurrentDomain);
        }

        Assert.Null(scope.CurrentDomain);
    }

    [Fact]
    public void BeginDomainScope_WithNull_ClearsCurrentDomain_ThenRestoresPrevious()
    {
        var scope = new AsyncLocalPropagationExecutionScope();
        using var _ = scope.BeginDomainScope(null);

        using (scope.BeginDomainScope("api"))
        {
            Assert.Equal("api", scope.CurrentDomain);

            using (scope.BeginDomainScope(null))
            {
                Assert.Null(scope.CurrentDomain);
            }

            Assert.Equal("api", scope.CurrentDomain);
        }
    }

    [Fact]
    public void BeginDomainScope_NestedScopes_RestoreInStackOrder()
    {
        var scope = new AsyncLocalPropagationExecutionScope();
        using var _ = scope.BeginDomainScope(null);

        using (scope.BeginDomainScope("outer"))
        {
            Assert.Equal("outer", scope.CurrentDomain);

            using (scope.BeginDomainScope("middle"))
            {
                Assert.Equal("middle", scope.CurrentDomain);

                using (scope.BeginDomainScope("inner"))
                {
                    Assert.Equal("inner", scope.CurrentDomain);
                }

                Assert.Equal("middle", scope.CurrentDomain);
            }

            Assert.Equal("outer", scope.CurrentDomain);
        }

        Assert.Null(scope.CurrentDomain);
    }

    [Fact]
    public void ScopeDisposal_IsIdempotent()
    {
        var scope = new AsyncLocalPropagationExecutionScope();
        using var _ = scope.BeginDomainScope(null);

        var disposable = scope.BeginDomainScope("once");
        Assert.Equal("once", scope.CurrentDomain);

        disposable.Dispose();
        Assert.Null(scope.CurrentDomain);

        // Should not throw or mutate state.
        disposable.Dispose();
        Assert.Null(scope.CurrentDomain);
    }

    [Fact]
    public async Task Domain_FlowsAcrossAwait_InSameAsyncExecution()
    {
        var scope = new AsyncLocalPropagationExecutionScope();
        using var _ = scope.BeginDomainScope(null);

        using (scope.BeginDomainScope("flow"))
        {
            await Task.Delay(10);
            Assert.Equal("flow", scope.CurrentDomain);
        }
    }

    [Fact]
    public async Task ChildTask_ReceivesAmbientDomain_FromParentExecutionContext()
    {
        var scope = new AsyncLocalPropagationExecutionScope();
        using var _ = scope.BeginDomainScope(null);

        using (scope.BeginDomainScope("parent"))
        {
            var childDomain = await Task.Run(() => scope.CurrentDomain);
            Assert.Equal("parent", childDomain);
        }
    }

    [Fact]
    public async Task ParallelExecutions_KeepDomainIsolation()
    {
        var scope = new AsyncLocalPropagationExecutionScope();
        using var _ = scope.BeginDomainScope(null);

        var results = new ConcurrentDictionary<int, string?>();
        var tasks = Enumerable.Range(1, 40).Select(async i =>
        {
            using (scope.BeginDomainScope($"domain-{i}"))
            {
                await Task.Delay(i % 7);
                results[i] = scope.CurrentDomain;
            }
        });

        await Task.WhenAll(tasks);

        foreach (var i in Enumerable.Range(1, 40))
        {
            Assert.Equal($"domain-{i}", results[i]);
        }
    }

    [Fact]
    public async Task DomainChanges_InChildTask_DoNotMutateParentCurrentDomain()
    {
        var scope = new AsyncLocalPropagationExecutionScope();
        using var _ = scope.BeginDomainScope(null);

        using (scope.BeginDomainScope("parent"))
        {
            await Task.Run(() =>
            {
                Assert.Equal("parent", scope.CurrentDomain);
                using var child = scope.BeginDomainScope("child");
                Assert.Equal("child", scope.CurrentDomain);
            });

            Assert.Equal("parent", scope.CurrentDomain);
        }
    }

    [Fact]
    public async Task UsingPattern_RestoresDomain_WhenExceptionThrown()
    {
        var scope = new AsyncLocalPropagationExecutionScope();
        using var _ = scope.BeginDomainScope(null);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using (scope.BeginDomainScope("faulted"))
            {
                await Task.Delay(5);
                throw new InvalidOperationException("boom");
            }
        });

        Assert.Null(scope.CurrentDomain);
    }

    [Fact]
    public void MultipleScopeInstances_ShareSameAmbientExecutionState()
    {
        var scope1 = new AsyncLocalPropagationExecutionScope();
        var scope2 = new AsyncLocalPropagationExecutionScope();
        using var _ = scope1.BeginDomainScope(null);

        using (scope1.BeginDomainScope("shared"))
        {
            Assert.Equal("shared", scope1.CurrentDomain);
            Assert.Equal("shared", scope2.CurrentDomain);
        }

        Assert.Null(scope1.CurrentDomain);
        Assert.Null(scope2.CurrentDomain);
    }
}
