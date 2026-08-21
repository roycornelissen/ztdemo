using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public static class QueueClientFactory
{
    public static QueueClient CreateQueueClient(ITokenCredentialProvider tokenCredentialProvider, IHostEnvironment environment, IConfiguration configuration, string queueName)
    {
        // Aspire injects the emulator connection string via WithReference() on the AppHost's Azure Storage queues resource.
        var aspireConnectionString = configuration.GetConnectionString("storage-queues");
        if (!string.IsNullOrWhiteSpace(aspireConnectionString))
        {
            var aspireQueueClient = new QueueClient(aspireConnectionString, queueName);
            aspireQueueClient.CreateIfNotExists();
            return aspireQueueClient;
        }

        if (environment.IsDevelopment())
        {
            if (string.IsNullOrWhiteSpace(configuration.GetSection("AzureStorage")["QueueEndpoint"]))
            {
                // Fall back to local storage emulator in development when running outside Aspire
                var devQueueClient = new QueueClient("UseDevelopmentStorage=true", queueName);
                devQueueClient.CreateIfNotExists();
                return devQueueClient;
            }
        }
        return new QueueClient(
            new Uri($"{configuration.GetSection("AzureStorage")["QueueEndpoint"]}{queueName}"),
            tokenCredentialProvider.Instance);
    }
}