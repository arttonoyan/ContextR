using ContextR.Grpc.IntegrationTests.Protos;
using Grpc.Core;

namespace ContextR.Grpc.IntegrationTests.Infrastructure;

internal sealed class CorrelationContext
{
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
}

internal sealed class ListPropagationContext
{
    public List<string>? Tags { get; set; }
}

internal sealed class ClassPropagationContext
{
    public PayloadValue? Payload { get; set; }
}

internal sealed class PayloadValue
{
    public string Code { get; set; } = string.Empty;

    public override string ToString() => Code;
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
        var listContext = _accessor.GetContext<ListPropagationContext>();
        var classContext = _accessor.GetContext<ClassPropagationContext>();
        return Task.FromResult(new ProbeReply
        {
            Message = request.Message,
            TraceId = correlation?.TraceId ?? string.Empty,
            SpanId = correlation?.SpanId ?? string.Empty,
            TagsHeader = context.RequestHeaders.GetValue("x-tags") ?? string.Empty,
            PayloadHeader = context.RequestHeaders.GetValue("x-payload") ?? string.Empty,
            HasListContext = listContext is not null,
            HasClassContext = classContext is not null
        });
    }
}
