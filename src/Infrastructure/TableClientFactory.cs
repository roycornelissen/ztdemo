using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public class TableClientFactory
{
    public static TableClient CreateTableClient(ITokenCredentialProvider tokenCredentialProvider, IHostEnvironment environment, IConfiguration configuration, string tableName)
    {
        // Aspire injects the emulator connection string via WithReference() on the AppHost's Azure Storage tables resource.
        var aspireConnectionString = configuration.GetConnectionString("storage-tables");
        if (!string.IsNullOrWhiteSpace(aspireConnectionString))
        {
            var aspireTableClient = new TableClient(aspireConnectionString, tableName);
            aspireTableClient.CreateIfNotExists();
            return aspireTableClient;
        }

        if (environment.IsDevelopment())
        {
            if (string.IsNullOrWhiteSpace(configuration.GetSection("AzureStorage")["TableEndpoint"]))
            {
                // Fall back to local storage emulator in development when running outside Aspire
                var devTableClient = new TableClient("UseDevelopmentStorage=true", tableName);
                devTableClient.CreateIfNotExists();
                return devTableClient;
            }
        }
        return new TableClient(
            new Uri(configuration.GetSection("AzureStorage")["TableEndpoint"]!),
            tableName,
            tokenCredentialProvider.Instance);
    }
}