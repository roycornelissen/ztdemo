using Azure.Core;
using Azure.Identity;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;

namespace PaymentsApiTests.Security;

public class StorageIdentityTests
{
    [TestCase("Production")]
    [TestCase("Staging")]
    [TestCase("UnexpectedEnvironment")]
    public void Connection_strings_cannot_bypass_workload_identity(string environmentName)
    {
        var config = Configuration(new()
        {
            ["ConnectionStrings:storage-queues"] = "not-a-valid-connection-string",
            ["ConnectionStrings:storage-tables"] = "not-a-valid-connection-string"
        });
        var environment = Environment(environmentName);
        var credentials = Substitute.For<ITokenCredentialProvider>();

        Should.Throw<InvalidOperationException>(() =>
            QueueClientFactory.CreateQueueClient(credentials, environment, config, "payments"));
        Should.Throw<InvalidOperationException>(() =>
            TableClientFactory.CreateTableClient(credentials, environment, config, "accounts"));
    }

    [TestCase(null)]
    [TestCase("not-a-uri")]
    [TestCase("http://storage.example.test")]
    [TestCase("https://storage.example.test?sig=not-allowed")]
    [TestCase("https://user:password@storage.example.test")]
    [TestCase("https://storage.example.test/#fragment")]
    public void Deployed_endpoints_must_use_https_without_embedded_credentials(string? endpoint)
    {
        var config = Configuration(new()
        {
            ["AzureStorage:QueueEndpoint"] = endpoint,
            ["AzureStorage:TableEndpoint"] = endpoint
        });
        var environment = Environment("Production");
        var credentials = Substitute.For<ITokenCredentialProvider>();

        Should.Throw<InvalidOperationException>(() =>
            QueueClientFactory.CreateQueueClient(credentials, environment, config, "payments"));
        Should.Throw<InvalidOperationException>(() =>
            TableClientFactory.CreateTableClient(credentials, environment, config, "accounts"));
    }

    [TestCase("https://storage.example.test")]
    [TestCase("https://storage.example.test/")]
    public void Identity_clients_are_constructed_without_provisioning_storage(string endpoint)
    {
        var config = Configuration(new()
        {
            ["AzureStorage:QueueEndpoint"] = endpoint,
            ["AzureStorage:TableEndpoint"] = endpoint
        });
        var environment = Environment("Production");
        var credentials = new TokenCredentialProvider(Substitute.For<TokenCredential>());

        var queue = QueueClientFactory.CreateQueueClient(credentials, environment, config, "payments");
        var table = TableClientFactory.CreateTableClient(credentials, environment, config, "accounts");

        queue.Uri.ShouldBe(new Uri("https://storage.example.test/payments"));
        table.Uri.ShouldBe(new Uri("https://storage.example.test/accounts"));
    }

    [TestCase("Production")]
    [TestCase("Staging")]
    public void Non_development_credentials_fail_closed_without_an_explicit_identity(string environmentName)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environmentName,
            DisableDefaults = true
        });
        Should.Throw<InvalidOperationException>(() => builder.CreateAzureCredential());

        builder.Configuration["AZURE_CLIENT_ID"] = "00000000-0000-0000-0000-000000000001";
        builder.CreateAzureCredential().ShouldBeOfType<ManagedIdentityCredential>();
    }

    [Test]
    public void Development_retains_the_focused_local_credential_chain()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
            DisableDefaults = true
        });
        builder.CreateAzureCredential().ShouldBeOfType<ChainedTokenCredential>();
    }

    [Test]
    public async Task Development_connection_strings_still_work_with_azurite()
    {
        var connectionString = System.Environment.GetEnvironmentVariable("MINIBANK_TEST_STORAGE_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Ignore("Set MINIBANK_TEST_STORAGE_CONNECTION_STRING to run the optional Azurite integration test.");
        }

        var config = Configuration(new()
        {
            ["ConnectionStrings:storage-queues"] = connectionString,
            ["ConnectionStrings:storage-tables"] = connectionString
        });
        var credentials = Substitute.For<ITokenCredentialProvider>();
        var suffix = Guid.NewGuid().ToString("N");
        var environment = Environment("Development");
        var queue = QueueClientFactory.CreateQueueClient(credentials, environment, config, $"zt{suffix}");
        try
        {
            var table = TableClientFactory.CreateTableClient(credentials, environment, config, $"zt{suffix}");
            try
            {
                await queue.SendMessageAsync("demo");
                var message = await queue.ReceiveMessageAsync();
                message.Value.Body.ToString().ShouldBe("demo");
                await table.AddEntityAsync(new Azure.Data.Tables.TableEntity("demo", "1"));
                var entity = await table.GetEntityAsync<Azure.Data.Tables.TableEntity>("demo", "1");
                entity.Value.RowKey.ShouldBe("1");
            }
            finally
            {
                await table.DeleteAsync();
            }
        }
        finally
        {
            await queue.DeleteIfExistsAsync();
        }
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IHostEnvironment Environment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return environment;
    }
}
