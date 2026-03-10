using System.Security.Cryptography;

namespace ContextR.Propagation.Signing.Internal;

internal sealed class SigningContextPropagator<TContext> : IContextPropagator<TContext>
    where TContext : class
{
    private readonly IContextPropagator<TContext> _inner;
    private readonly ISigningKeyProvider _keyProvider;
    private readonly SigningOptions _options;
    private readonly ContextPropagationFailureHandlerRegistry<TContext>? _failureRegistry;
    private readonly IPropagationExecutionScope _executionScope;
    private readonly IServiceProvider _services;

    public SigningContextPropagator(
        IContextPropagator<TContext> inner,
        ISigningKeyProvider keyProvider,
        SigningOptions options,
        IServiceProvider services,
        IPropagationExecutionScope? executionScope = null,
        ContextPropagationFailureHandlerRegistry<TContext>? failureRegistry = null)
    {
        _inner = inner;
        _keyProvider = keyProvider;
        _options = options;
        _services = services;
        _executionScope = executionScope ?? new AsyncLocalPropagationExecutionScope();
        _failureRegistry = failureRegistry;
    }

    public void Inject<TCarrier>(TContext context, TCarrier carrier, Action<TCarrier, string, string> setter)
    {
        var captured = new Dictionary<string, string>(StringComparer.Ordinal);

        _inner.Inject(context, captured, static (dict, key, value) => dict[key] = value);

        foreach (var kv in captured)
            setter(carrier, kv.Key, kv.Value);

        if (captured.Count == 0)
            return;

        int keyVersion;
        byte[] key;
        try
        {
            keyVersion = _keyProvider.GetCurrentVersion(_options.KeyId!);
            key = _keyProvider.GetKey(_options.KeyId!, keyVersion);
        }
        catch (Exception ex)
        {
            HandleFailure(
                _options.SignatureHeader,
                PropagationDirection.Inject,
                PropagationFailureReason.Unexpected,
                SigningFailureReasons.KeyNotFound,
                exception: ex);
            return;
        }

        var signingInput = CanonicalSigningInput.Build(captured);
        var hmac = HMACSHA256.HashData(key, signingInput);
        var signatureValue = SignatureCodec.Encode(hmac, keyVersion);

        setter(carrier, _options.SignatureHeader, signatureValue);
    }

    public TContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, string?> getter)
    {
        var signatureHeaderValue = getter(carrier, _options.SignatureHeader);
        if (signatureHeaderValue is null)
        {
            HandleFailure(
                _options.SignatureHeader,
                PropagationDirection.Extract,
                PropagationFailureReason.MissingRequired,
                SigningFailureReasons.SignatureMissing);
            return null;
        }

        if (!SignatureCodec.TryDecode(signatureHeaderValue, out var receivedHmac, out var keyVersion))
        {
            HandleFailure(
                _options.SignatureHeader,
                PropagationDirection.Extract,
                PropagationFailureReason.ParseFailed,
                SigningFailureReasons.SignatureMalformed,
                rawValue: signatureHeaderValue);
            return null;
        }

        byte[] key;
        try
        {
            key = _keyProvider.GetKey(_options.KeyId!, keyVersion);
        }
        catch (Exception ex)
        {
            HandleFailure(
                _options.SignatureHeader,
                PropagationDirection.Extract,
                PropagationFailureReason.Unexpected,
                SigningFailureReasons.KeyNotFound,
                exception: ex);
            return null;
        }

        var captured = new Dictionary<string, string>(StringComparer.Ordinal);

        string? InterceptingGetter(TCarrier c, string headerKey)
        {
            if (string.Equals(headerKey, _options.SignatureHeader, StringComparison.Ordinal))
                return null;

            var value = getter(c, headerKey);
            if (value is not null)
                captured[headerKey] = value;

            return value;
        }

        var context = _inner.Extract(carrier, InterceptingGetter);
        if (context is null)
            return null;

        var signingInput = CanonicalSigningInput.Build(captured);
        var expectedHmac = HMACSHA256.HashData(key, signingInput);

        if (!CryptographicOperations.FixedTimeEquals(expectedHmac, receivedHmac))
        {
            HandleFailure(
                _options.SignatureHeader,
                PropagationDirection.Extract,
                PropagationFailureReason.ParseFailed,
                SigningFailureReasons.SignatureInvalid,
                rawValue: signatureHeaderValue);
            return null;
        }

        return context;
    }

    private void HandleFailure(
        string key,
        PropagationDirection direction,
        PropagationFailureReason reason,
        string signingReason,
        string? rawValue = null,
        Exception? exception = null)
    {
        var failure = new PropagationFailureContext
        {
            ContextType = typeof(TContext),
            Key = key,
            Direction = direction,
            Reason = reason,
            Domain = _executionScope.CurrentDomain,
            RawValue = rawValue ?? signingReason,
            Exception = exception
        };

        var handler = _failureRegistry?.Resolve(_services, failure.Domain);
        var action = handler?.Handle(failure) ?? PropagationFailureAction.Throw;

        if (action == PropagationFailureAction.Throw)
        {
            if (exception is not null)
                throw exception;

            throw new InvalidOperationException(
                $"Context signing {direction.ToString().ToLowerInvariant()} failed for '{key}': {signingReason}.");
        }
    }
}
