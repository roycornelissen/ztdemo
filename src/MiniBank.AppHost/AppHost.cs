#pragma warning disable ASPIRETERMINAL001

using Azure.Data.Tables;

var builder = DistributedApplication.CreateBuilder(args);

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
    await accounts.UpsertEntityAsync(
        new TableEntity("accounts", "1")
        {
            ["Description"] = "Everyday account",
            ["UserId"] = "roy_cornelissen@hotmail.com"
        },
        cancellationToken: cancellationToken);
    await accounts.UpsertEntityAsync(
        new TableEntity("accounts", "2")
        {
            ["Description"] = "Savings account",
            ["UserId"] = "test-user"
        },
        cancellationToken: cancellationToken);

    await tableServiceClient.CreateTableIfNotExistsAsync("transactions", cancellationToken);
});

var accountsApi = builder.AddProject<Projects.AccountsApi>("accountsapi")
    .WithReference(accountsTable)
    .WaitFor(accountsTable)
    .WithHttpHealthCheck("/healthz")
    .WithUrlForEndpoint("https", url =>
    {
        url.DisplayText = "Swagger";
        url.Url = "/swagger";
    });

var paymentsApi = builder.AddProject<Projects.PaymentsApi>("paymentsapi")
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
