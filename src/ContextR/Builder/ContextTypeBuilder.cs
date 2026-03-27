using Microsoft.Extensions.DependencyInjection;

namespace ContextR;

internal sealed class ContextTypeBuilder<TContext> : IContextTypeBuilder<TContext>
    where TContext : class
{
    public ContextTypeBuilder(IServiceCollection services, string? domain)
    {
        Services = services;
        Domain = domain;
    }

    public IServiceCollection Services { get; }

    public string? Domain { get; }
}
