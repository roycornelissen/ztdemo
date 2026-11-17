using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace PaymentsApi.Security;

internal sealed class BindingFailureSecurityFilter : IEndpointFilter
{
    private readonly ILogger logger;

    public BindingFailureSecurityFilter(ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger("MiniBank.Security");
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        if (httpContext.Response.StatusCode == StatusCodes.Status400BadRequest)
        {
            var route = (httpContext.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            logger.LogWarning(new EventId(1003, "PaymentBindingFailure"),
                "Request binding failed with {StatusCode} on {Route}; trace {TraceId}",
                httpContext.Response.StatusCode, route, Activity.Current?.TraceId.ToString());
        }

        return await next(context);
    }
}
