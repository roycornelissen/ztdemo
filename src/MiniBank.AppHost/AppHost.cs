#pragma warning disable ASPIRETERMINAL001

using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);
var (demoOwnerTenantId, demoOwnerObjectIds) = GetDemoOwners(builder.Configuration);

// Local emulator for Azure Storage (Queues + Tables) used by AccountsApi, PaymentsApi and Processing.
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite => azurite.WithDataVolume());

var storageQueues = storage.AddQueues("storage-queues");
var paymentsQueue = storage.AddQueue("payments");
var accountsTable = storage.AddTables("storage-tables");

storage.OnResourceReady(async (_, _, cancellationToken) =>
{
    var connectionString = await accountsTable.Resource.ConnectionStringExpression
        .GetValueAsync(cancellationToken);

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("The Azure Table Storage connection string is unavailable.");
    }

    var tableServiceClient = new TableServiceClient(connectionString);
    var accounts = tableServiceClient.GetTableClient("accounts");

    await accounts.CreateIfNotExistsAsync(cancellationToken);
    for (var ownerIndex = 0; ownerIndex < demoOwnerObjectIds.Length; ownerIndex++)
    {
        await accounts.UpsertEntityAsync(
            new TableEntity("accounts", (ownerIndex * 2 + 1).ToString())
            {
                ["Description"] = "Everyday account",
                ["OwnerTenantId"] = demoOwnerTenantId.ToString("D"),
                ["OwnerObjectId"] = demoOwnerObjectIds[ownerIndex].ToString("D")
            },
            cancellationToken: cancellationToken);
        await accounts.UpsertEntityAsync(
            new TableEntity("accounts", (ownerIndex * 2 + 2).ToString())
            {
                ["Description"] = "Savings account",
                ["OwnerTenantId"] = demoOwnerTenantId.ToString("D"),
                ["OwnerObjectId"] = demoOwnerObjectIds[ownerIndex].ToString("D")
            },
            cancellationToken: cancellationToken);
    }

    await tableServiceClient.CreateTableIfNotExistsAsync("transactions", cancellationToken);
});

var accountsApi = builder.AddProject<Projects.AccountsApi>("accountsapi")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithReference(accountsTable)
    .WaitFor(accountsTable)
    .WithHttpHealthCheck("/healthz")
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = "Swagger";
        url.Url = "/swagger";
    });

var paymentsApi = builder.AddProject<Projects.PaymentsApi>("paymentsapi")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithReference(storageQueues)
    .WithReference(accountsTable)
    .WaitFor(paymentsQueue)
    .WaitFor(accountsTable)
    .WithHttpHealthCheck("/healthz")
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = "Swagger";
        url.Url = "/swagger";
    });

builder.AddProject<Projects.Processing>("processing")
    .WithReference(storageQueues)
    .WithReference(accountsTable)
    .WaitFor(paymentsQueue)
    .WaitFor(accountsTable)
    .WithHttpHealthCheck("/healthz");

builder.Build().Run();

static (Guid TenantId, Guid[] ObjectIds) GetDemoOwners(IConfiguration configuration)
{
    if (!Guid.TryParse(configuration["MiniBank:DemoOwnerTenantId"], out var tenantId) ||
        tenantId == Guid.Empty)
    {
        throw new InvalidOperationException(
            "MiniBank:DemoOwnerTenantId must be set to the demo user's Entra tenant ID.");
    }

    if (!Guid.TryParse(configuration["MiniBank:DemoOwnerObjectId"], out var objectId) ||
        objectId == Guid.Empty)
    {
        throw new InvalidOperationException(
            "MiniBank:DemoOwnerObjectId must be set to the demo user's Entra object ID.");
    }

    var additionalOwner = configuration["MiniBank:AdditionalDemoOwnerObjectId"];
    if (additionalOwner is null)
    {
        return (tenantId, [objectId]);
    }

    if (!Guid.TryParse(additionalOwner, out var additionalObjectId) ||
        additionalObjectId == Guid.Empty)
    {
        throw new InvalidOperationException(
            "MiniBank:AdditionalDemoOwnerObjectId must be a non-empty Entra object ID when configured.");
    }

    return (tenantId, [objectId, additionalObjectId]);
}
