using Grpc.Core;
using Grpc.Core.Interceptors;
using ContextR.Propagation;

namespace ContextR.Transport.Grpc;

/// <summary>
/// A gRPC client <see cref="Interceptor"/> that propagates the current ambient context
/// into outgoing request metadata using the registered <see cref="IContextPropagator{TContext}"/>.
/// </summary>
/// <typeparam name="TContext">The context type to propagate.</typeparam>
public class ContextPropagationInterceptor<TContext> : Interceptor
    where TContext : class
{
    private readonly IContextAccessor _accessor;
    private readonly IContextPropagator<TContext> _propagator;
    private readonly IPropagationExecutionScope _executionScope;
    private readonly string? _domain;

    /// <inheritdoc cref="ContextPropagationInterceptor{TContext}" />
    public ContextPropagationInterceptor(
        IContextAccessor accessor,
        IContextPropagator<TContext> propagator)
        : this(accessor, propagator, domain: null, executionScope: new AsyncLocalPropagationExecutionScope())
    {
    }

    internal ContextPropagationInterceptor(
        IContextAccessor accessor,
        IContextPropagator<TContext> propagator,
        string? domain,
        IPropagationExecutionScope? executionScope = null)
    {
        _accessor = accessor;
        _propagator = propagator;
        _executionScope = executionScope ?? new AsyncLocalPropagationExecutionScope();
        _domain = domain;
    }

    /// <inheritdoc />
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var nextContext = TryCreateContext(context);
        return base.AsyncUnaryCall(request, nextContext ?? context, continuation);
    }

    /// <inheritdoc />
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var nextContext = TryCreateContext(context);
        return base.BlockingUnaryCall(request, nextContext ?? context, continuation);
    }

    /// <inheritdoc />
    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var nextContext = TryCreateContext(context);
        return base.AsyncServerStreamingCall(request, nextContext ?? context, continuation);
    }

    /// <inheritdoc />
    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var nextContext = TryCreateContext(context);
        return base.AsyncClientStreamingCall(nextContext ?? context, continuation);
    }

    /// <inheritdoc />
    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var nextContext = TryCreateContext(context);
        return base.AsyncDuplexStreamingCall(nextContext ?? context, continuation);
    }

    private ClientInterceptorContext<TRequest, TResponse>? TryCreateContext<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        var currentContext = _domain is not null
            ? _accessor.GetContext<TContext>(_domain)
            : _accessor.GetContext<TContext>();
        if (currentContext is null)
        {
            return null;
        }

        var headers = context.Options.Headers is null
            ? new Metadata()
            : CloneMetadata(context.Options.Headers);

        using var _ = _executionScope.BeginDomainScope(_domain);
        _propagator.Inject(
            currentContext,
            headers,
            static (metadata, key, value) => metadata.Add(key.ToLowerInvariant(), value));

        var options = context.Options.WithHeaders(headers);
        return new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);
    }

    private static Metadata CloneMetadata(Metadata source)
    {
        var clone = new Metadata();
        foreach (var entry in source)
        {
            if (entry.IsBinary)
            {
                clone.Add(entry.Key, entry.ValueBytes);
            }
            else
            {
                clone.Add(entry.Key, entry.Value);
            }
        }

        return clone;
    }
}
