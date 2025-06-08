using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public static class QueueClientFactory
{
    public static QueueClient CreateQueueClient(ITokenCredentialProvider tokenCredentialProvider, IHostEnvironment environment, IConfiguration configuration, string queueName)
    {
        if (environment.IsDevelopment())
        {
            if (string.IsNullOrWhiteSpace(configuration.GetSection("AzureStorage")["QueueEndpoint"]))
            {
                // Use local storage emulator in development
                return new QueueClient("UseDevelopmentStorage=true", queueName);
            }
        }
        return new QueueClient(
            new Uri($"{configuration.GetSection("AzureStorage")["QueueEndpoint"]}{queueName}"),
            tokenCredentialProvider.Instance);
    }
}