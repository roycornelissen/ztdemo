using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public class TableClientFactory
{
    public static TableClient CreateTableClient(ITokenCredentialProvider tokenCredentialProvider, IHostEnvironment environment, IConfiguration configuration, string tableName)
    {
        var connectionString = StorageClientConfiguration.GetDevelopmentConnectionString(
            environment, configuration, "storage-tables", "AzureStorage:TableEndpoint");
        if (connectionString is not null)
        {
            var client = new TableClient(connectionString, tableName);
            client.CreateIfNotExists();
            return client;
        }

        return new TableClient(
            StorageClientConfiguration.GetServiceEndpoint(environment, configuration, "AzureStorage:TableEndpoint"),
            tableName,
            tokenCredentialProvider.Instance);
    }
}