using ContextR.Propagation.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Propagation.Signing.UnitTests.Infrastructure;

internal static class TestHelper
{
    internal static IContextPropagator<TestContext> BuildPropagator(
        byte[] key,
        Action<SigningOptions>? configureOptions = null,
        Func<PropagationFailureContext, PropagationFailureAction>? onFailure = null)
    {
        var services = new ServiceCollection();

        services.AddContextR(ctx =>
        {
            ctx.Add<TestContext>(reg =>
            {
                reg.MapProperty(c => c.TenantId, "X-Tenant-Id")
                   .MapProperty(c => c.Region, "X-Region")
                   .UseContextSigning<TestContext>(configureOptions ?? (o => o.Key = key));

                if (onFailure is not null)
                    reg.OnPropagationFailure<TestContext>(onFailure);
            });
        });

        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IContextPropagator<TestContext>>();
    }

    internal static IContextPropagator<TestContext> BuildPropagatorWithProvider(
        ISigningKeyProvider keyProvider,
        Action<SigningOptions> configureOptions,
        Func<PropagationFailureContext, PropagationFailureAction>? onFailure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(keyProvider);

        services.AddContextR(ctx =>
        {
            ctx.Add<TestContext>(reg =>
            {
                reg.MapProperty(c => c.TenantId, "X-Tenant-Id")
                   .MapProperty(c => c.Region, "X-Region")
                   .UseContextSigning<TestContext>(configureOptions);

                if (onFailure is not null)
                    reg.OnPropagationFailure<TestContext>(onFailure);
            });
        });

        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IContextPropagator<TestContext>>();
    }

    internal static Dictionary<string, string> InjectToHeaders(
        IContextPropagator<TestContext> propagator,
        TestContext context)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        propagator.Inject(context, headers, static (dict, key, value) => dict[key] = value);
        return headers;
    }

    internal static TestContext? ExtractFromHeaders(
        IContextPropagator<TestContext> propagator,
        Dictionary<string, string> headers)
    {
        return propagator.Extract(headers,
            static (dict, key) => dict.TryGetValue(key, out var v) ? v : null);
    }
}
