using System.Linq.Expressions;
using ContextR.OpenFeature.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.OpenFeature;

/// <summary>
/// Configures ContextR to OpenFeature EvaluationContext mapping behavior.
/// </summary>
public class ContextROpenFeatureOptions
{
    private readonly List<ContextMappingRegistration> _registrations = [];
    private readonly HashSet<string> _allowedKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _blockedKeys = new(StringComparer.Ordinal);

    internal IReadOnlyList<ContextMappingRegistration> Registrations => _registrations;
    internal IReadOnlySet<string> AllowedKeys => _allowedKeys;
    internal IReadOnlySet<string> BlockedKeys => _blockedKeys;

    internal Func<IServiceProvider, string?>? TargetingKeyFactory { get; private set; }
    internal Func<IServiceProvider, string?>? ContextKindFactory { get; private set; }

    /// <summary>
    /// Default ContextR domain used by typed mapping APIs when no explicit domain is provided.
    /// </summary>
    public string? DefaultDomain { get; private set; }

    /// <summary>
    /// Gets or sets duplicate key behavior for mapped attributes.
    /// </summary>
    public ContextROpenFeatureCollisionBehavior CollisionBehavior { get; set; } = ContextROpenFeatureCollisionBehavior.LastWriteWins;

    /// <summary>
    /// Gets or sets behavior for unsupported value conversion types.
    /// </summary>
    public ContextROpenFeatureUnsupportedValueBehavior UnsupportedValueBehavior { get; set; } = ContextROpenFeatureUnsupportedValueBehavior.Ignore;

    /// <summary>
    /// Gets or sets whether null values are written as explicit null attributes.
    /// </summary>
    public bool IncludeNullValues { get; set; }

    /// <summary>
    /// Sets the default ContextR domain for typed mapping APIs.
    /// </summary>
    public ContextROpenFeatureOptions UseDomain(string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        DefaultDomain = domain;
        return this;
    }

    /// <summary>
    /// Restricts mappings to a specific set of attribute keys.
    /// </summary>
    public ContextROpenFeatureOptions AllowKeys(params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        foreach (var key in keys)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            _allowedKeys.Add(key);
        }

        return this;
    }

    /// <summary>
    /// Blocks specific attribute keys from being written.
    /// </summary>
    public ContextROpenFeatureOptions BlockKeys(params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        foreach (var key in keys)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            _blockedKeys.Add(key);
        }

        return this;
    }

    /// <summary>
    /// Sets the targeting key using a typed context selector.
    /// </summary>
    public ContextROpenFeatureOptions SetTargetingKey<TContext>(
        Expression<Func<TContext, string?>> selector,
        string? domain = null)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(selector);

        var compiled = selector.Compile();
        var effectiveDomain = domain ?? DefaultDomain;
        TargetingKeyFactory = sp =>
        {
            var accessor = sp.GetRequiredService<IContextAccessor>();
            var context = effectiveDomain is null
                ? accessor.GetContext<TContext>()
                : accessor.GetContext<TContext>(effectiveDomain);

            return context is null ? null : compiled(context);
        };

        return this;
    }

    /// <summary>
    /// Sets the targeting key using a custom factory.
    /// </summary>
    public ContextROpenFeatureOptions SetTargetingKey(Func<IServiceProvider, string?> targetingKeyFactory)
    {
        ArgumentNullException.ThrowIfNull(targetingKeyFactory);
        TargetingKeyFactory = targetingKeyFactory;
        return this;
    }

    /// <summary>
    /// Sets a static context kind attribute value.
    /// </summary>
    public ContextROpenFeatureOptions SetContextKind(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ContextKindFactory = _ => kind;
        return this;
    }

    /// <summary>
    /// Sets context kind using a custom factory.
    /// </summary>
    public ContextROpenFeatureOptions SetContextKind(Func<IServiceProvider, string?> kindFactory)
    {
        ArgumentNullException.ThrowIfNull(kindFactory);
        ContextKindFactory = kindFactory;
        return this;
    }

    /// <summary>
    /// Configures mapping for a context type in the default domain.
    /// </summary>
    public ContextROpenFeatureOptions Map<TContext>(Action<ContextROpenFeatureMapBuilder<TContext>> configure)
        where TContext : class
    {
        return Map(DefaultDomain, configure);
    }

    /// <summary>
    /// Configures mapping for a context type in the specified domain.
    /// </summary>
    public ContextROpenFeatureOptions Map<TContext>(
        string? domain,
        Action<ContextROpenFeatureMapBuilder<TContext>> configure)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ContextROpenFeatureMapBuilder<TContext>();
        configure(builder);
        _registrations.Add(ContextMappingRegistration.Create(domain, builder));
        return this;
    }
}

/// <summary>
/// Typed convenience configuration surface for single-context setup.
/// </summary>
public sealed class ContextROpenFeatureOptions<TContext> : ContextROpenFeatureOptions
    where TContext : class
{
    /// <summary>
    /// Sets targeting key from the primary context type.
    /// </summary>
    public ContextROpenFeatureOptions<TContext> SetTargetingKey(Expression<Func<TContext, string?>> selector)
    {
        base.SetTargetingKey(selector);
        return this;
    }

    /// <summary>
    /// Configures mapping for the primary context type.
    /// </summary>
    public ContextROpenFeatureOptions<TContext> Map(Action<ContextROpenFeatureMapBuilder<TContext>> configure)
    {
        base.Map(configure);
        return this;
    }

    /// <summary>
    /// Configures mapping for an additional context type.
    /// </summary>
    public new ContextROpenFeatureOptions<TContext> Map<TAdditional>(
        Action<ContextROpenFeatureMapBuilder<TAdditional>> configure)
        where TAdditional : class
    {
        base.Map(configure);
        return this;
    }
}
