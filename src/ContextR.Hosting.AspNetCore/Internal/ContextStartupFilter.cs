using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ContextR.Hosting.AspNetCore.Internal;

internal sealed class ContextStartupFilter<TContext> : IStartupFilter
    where TContext : class
{
    private readonly string? _domain;

    public ContextStartupFilter(string? domain)
    {
        _domain = domain;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            if (_domain is not null)
                app.UseMiddleware<ContextMiddleware<TContext>>(_domain);
            else
                app.UseMiddleware<ContextMiddleware<TContext>>();

            next(app);
        };
    }
}
