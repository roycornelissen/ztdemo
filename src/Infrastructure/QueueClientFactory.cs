using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public static class QueueClientFactory
{
    public static QueueClient CreateQueueClient(ITokenCredentialProvider tokenCredentialProvider, IHostEnvironment environment, IConfiguration configuration, string queueName)
    {
        var connectionString = StorageClientConfiguration.GetDevelopmentConnectionString(
            environment, configuration, "storage-queues", "AzureStorage:QueueEndpoint");
        if (connectionString is not null)
        {
            var client = new QueueClient(connectionString, queueName);
            client.CreateIfNotExists();
            return client;
        }

        return new QueueClient(
            new Uri(StorageClientConfiguration.GetServiceEndpoint(environment, configuration, "AzureStorage:QueueEndpoint"), queueName),
            tokenCredentialProvider.Instance);
    }
}