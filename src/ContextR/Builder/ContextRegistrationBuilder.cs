using Microsoft.Extensions.DependencyInjection;

namespace ContextR;

internal sealed class ContextRegistrationBuilder<TContext> : IContextRegistrationBuilder<TContext>
    where TContext : class
{
    public ContextRegistrationBuilder(IServiceCollection services, string? domain)
    {
        Services = services;
        Domain = domain;
    }

    public IServiceCollection Services { get; }

    public string? Domain { get; }
}
