using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public static class ApiRequestBudgetExtensions
{
    public const int MaximumBodyBytes = 4096;
    public const int RequestsPerMinute = 60;

    public static WebApplicationBuilder AddApiRequestBudgets(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = MaximumBodyBytes;
            options.Limits.MaxRequestHeadersTotalSize = 16 * 1024;
            options.Limits.MaxRequestHeaderCount = 32;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
        });

        builder.Services.AddRateLimiter(options =>
        {
            // A single partition bounds limiter memory even with attacker-controlled identities/IPs.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                RateLimitPartition.GetFixedWindowLimiter("api", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = RequestsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await Results.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Request budget exhausted. Retry later.")
                    .ExecuteAsync(context.HttpContext);
            };
        });
        return builder;
    }
}
