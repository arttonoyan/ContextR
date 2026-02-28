using ContextR.Grpc.IntegrationTests.Infrastructure;
using ContextR.Grpc.IntegrationTests.Protos;
using Grpc.Core;

namespace ContextR.Grpc.IntegrationTests;

public sealed class GrpcContextPropagationIntegrationTests
{
    [Fact]
    public async Task ServerInterceptor_ExtractsContext_FromIncomingGrpcMetadata()
    {
        await using var cluster = await GrpcTestCluster.CreateAsync();
        var client = cluster.CreateBackendDirectClient();

        var headers = new Metadata
        {
            { "x-trace-id", "trace-from-client" },
            { "x-span-id", "span-from-client" }
        };

        var reply = await client.EchoAsync(new ProbeRequest { Message = "direct" }, headers);

        Assert.Equal("direct", reply.Message);
        Assert.Equal("trace-from-client", reply.TraceId);
        Assert.Equal("span-from-client", reply.SpanId);
    }

    [Fact]
    public async Task GlobalGrpcPropagation_InjectsContext_IntoOutgoingGrpcCall()
    {
        await using var cluster = await GrpcTestCluster.CreateAsync();

        var json = await cluster.GetRelayJsonAsync(
            ("x-trace-id", "trace-via-frontend"),
            ("x-span-id", "span-via-frontend"));

        Assert.Equal("relay", json.GetProperty("message").GetString());
        Assert.Equal("trace-via-frontend", json.GetProperty("traceId").GetString());
        Assert.Equal("span-via-frontend", json.GetProperty("spanId").GetString());
    }
}
