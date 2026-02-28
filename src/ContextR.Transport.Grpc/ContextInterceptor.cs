using Grpc.Core;
using Grpc.Core.Interceptors;
using ContextR.Propagation.Abstractions;

namespace ContextR.Transport.Grpc;

/// <summary>
/// A gRPC server <see cref="Interceptor"/> that extracts context from incoming
/// request metadata and writes it into ambient ContextR storage.
/// </summary>
/// <typeparam name="TContext">The context type to extract.</typeparam>
public class ContextInterceptor<TContext> : Interceptor
    where TContext : class
{
    private readonly IContextWriter _writer;
    private readonly IContextPropagator<TContext> _propagator;
    private readonly string? _domain;

    /// <inheritdoc cref="ContextInterceptor{TContext}" />
    public ContextInterceptor(
        IContextWriter writer,
        IContextPropagator<TContext> propagator)
        : this(writer, propagator, domain: null)
    {
    }

    internal ContextInterceptor(
        IContextWriter writer,
        IContextPropagator<TContext> propagator,
        string? domain)
    {
        _writer = writer;
        _propagator = propagator;
        _domain = domain;
    }

    /// <inheritdoc />
    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ExtractAndSetContext(context.RequestHeaders);
        return continuation(request, context);
    }

    /// <inheritdoc />
    public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ExtractAndSetContext(context.RequestHeaders);
        return continuation(requestStream, context);
    }

    /// <inheritdoc />
    public override Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ExtractAndSetContext(context.RequestHeaders);
        return continuation(request, responseStream, context);
    }

    /// <inheritdoc />
    public override Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ExtractAndSetContext(context.RequestHeaders);
        return continuation(requestStream, responseStream, context);
    }

    private void ExtractAndSetContext(Metadata headers)
    {
        var extractedContext = _propagator.ExtractContext(headers);
        if (extractedContext is null)
        {
            return;
        }

        if (_domain is not null)
        {
            _writer.SetContext(_domain, extractedContext);
        }
        else
        {
            _writer.SetContext(extractedContext);
        }
    }
}
