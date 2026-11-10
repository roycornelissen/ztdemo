using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting;

public static class SecurityEventExtensions
{
    public static WebApplication UseSecurityEventLogging(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MiniBank.Security");
        app.Use(async (context, next) =>
        {
            await next(context);

            if (context.Response.StatusCode is 401 or 403 or 429)
            {
                var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
                logger.LogWarning(new EventId(1000, "RequestRejected"),
                    "Request rejected with {StatusCode} on {Route}; trace {TraceId}",
                    context.Response.StatusCode, route, Activity.Current?.TraceId.ToString());
            }
        });
        return app;
    }
}
