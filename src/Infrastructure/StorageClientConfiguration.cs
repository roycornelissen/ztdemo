using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

internal static class StorageClientConfiguration
{
    public static string? GetDevelopmentConnectionString(
        IHostEnvironment environment, IConfiguration configuration, string connectionName, string endpointKey)
    {
        var connectionString = configuration.GetConnectionString(connectionName);
        if (!environment.IsDevelopment())
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"ConnectionStrings:{connectionName} is only supported in Development. Configure a storage endpoint and managed identity.");
            }
            return null;
        }

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        return string.IsNullOrWhiteSpace(configuration[endpointKey]) ? "UseDevelopmentStorage=true" : null;
    }

    public static Uri GetServiceEndpoint(IHostEnvironment environment, IConfiguration configuration, string key)
    {
        if (!Uri.TryCreate(configuration[key], UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttps && !(environment.IsDevelopment() && endpoint.Scheme == Uri.UriSchemeHttp))
            || !string.IsNullOrEmpty(endpoint.UserInfo)
            || !string.IsNullOrEmpty(endpoint.Query)
            || !string.IsNullOrEmpty(endpoint.Fragment))
        {
            throw new InvalidOperationException(
                $"{key} must be an absolute HTTPS service endpoint without credentials, query or fragment (HTTP is allowed only in Development).");
        }

        return new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/");
    }
}
