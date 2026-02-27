using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ContextR.AspNetCore.Internal;

internal sealed class ContextStartupFilter<TContext> : IStartupFilter
    where TContext : class
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<ContextMiddleware<TContext>>();
            next(app);
        };
    }
}
