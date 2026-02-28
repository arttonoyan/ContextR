using ContextR.Grpc.IntegrationTests.Infrastructure;

namespace ContextR.Grpc.IntegrationTests;

public sealed class GrpcContextPropagationFunctionalTests
{
    [Fact]
    public async Task FullRoundTrip_HttpHeaders_AppearAsBackendGrpcContext()
    {
        await using var cluster = await GrpcTestCluster.CreateAsync();

        var trace = Guid.NewGuid().ToString("N");
        var span = Guid.NewGuid().ToString("N");

        var json = await cluster.GetRelayJsonAsync(
            ("x-trace-id", trace),
            ("x-span-id", span));

        Assert.Equal(trace, json.GetProperty("traceId").GetString());
        Assert.Equal(span, json.GetProperty("spanId").GetString());
    }

    [Fact]
    public async Task MissingContext_DoesNotPropagateValues()
    {
        await using var cluster = await GrpcTestCluster.CreateAsync();

        var json = await cluster.GetRelayJsonAsync();

        Assert.Equal(string.Empty, json.GetProperty("traceId").GetString());
        Assert.Equal(string.Empty, json.GetProperty("spanId").GetString());
    }

    [Fact]
    public async Task ConcurrentRelayRequests_KeepContextIsolation()
    {
        await using var cluster = await GrpcTestCluster.CreateAsync();

        var tasks = Enumerable.Range(1, 30).Select(async i =>
        {
            var trace = $"trace-{i}";
            var span = $"span-{i}";
            var json = await cluster.GetRelayJsonAsync(
                ("x-trace-id", trace),
                ("x-span-id", span));

            return new
            {
                ExpectedTrace = trace,
                ActualTrace = json.GetProperty("traceId").GetString(),
                ExpectedSpan = span,
                ActualSpan = json.GetProperty("spanId").GetString()
            };
        });

        var results = await Task.WhenAll(tasks);
        foreach (var result in results)
        {
            Assert.Equal(result.ExpectedTrace, result.ActualTrace);
            Assert.Equal(result.ExpectedSpan, result.ActualSpan);
        }
    }
}
