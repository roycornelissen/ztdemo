using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public class TableClientFactory
{
    public static TableClient CreateTableClient(ITokenCredentialProvider tokenCredentialProvider, IHostEnvironment environment, IConfiguration configuration, string tableName)
    {
        if (environment.IsDevelopment())
        {
            if (string.IsNullOrWhiteSpace(configuration.GetSection("AzureStorage")["TableEndpoint"]))
            {
                // Use local storage emulator in development
                return new TableClient("UseDevelopmentStorage=true", tableName);
            }
        }
        return new TableClient(
            new Uri(configuration.GetSection("AzureStorage")["TableEndpoint"]!),
            tableName,
            tokenCredentialProvider.Instance);
    }
}