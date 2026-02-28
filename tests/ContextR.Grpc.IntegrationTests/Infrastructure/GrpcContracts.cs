using ContextR.Grpc.IntegrationTests.Protos;
using Grpc.Core;

namespace ContextR.Grpc.IntegrationTests.Infrastructure;

internal sealed class CorrelationContext
{
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
}

internal sealed class GrpcProbeService : GrpcProbe.GrpcProbeBase
{
    private readonly IContextAccessor _accessor;

    public GrpcProbeService(IContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public override Task<ProbeReply> Echo(ProbeRequest request, ServerCallContext context)
    {
        var correlation = _accessor.GetContext<CorrelationContext>();
        return Task.FromResult(new ProbeReply
        {
            Message = request.Message,
            TraceId = correlation?.TraceId ?? string.Empty,
            SpanId = correlation?.SpanId ?? string.Empty
        });
    }
}
