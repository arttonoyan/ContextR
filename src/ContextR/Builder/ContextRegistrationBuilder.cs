using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

    public IContextRegistrationBuilder<TContext> UsePropagator<TPropagator>()
        where TPropagator : class, IContextPropagator<TContext>
    {
        Services.TryAddSingleton<IContextPropagator<TContext>, TPropagator>();
        return this;
    }
}
