var builder = DistributedApplication.CreateBuilder(args);

// Local emulator for Azure Storage (Queues + Tables) used by AccountsApi, PaymentsApi and Processing.
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite => azurite.WithDataVolume());

var paymentsQueue = storage.AddQueues("storage-queues");
var accountsTable = storage.AddTables("storage-tables");

var accountsApi = builder.AddProject<Projects.AccountsApi>("accountsapi")
    .WithReference(accountsTable)
    .WaitFor(accountsTable)
    .WithHttpHealthCheck("/healthz");

var paymentsApi = builder.AddProject<Projects.PaymentsApi>("paymentsapi")
    .WithReference(paymentsQueue)
    .WithReference(accountsTable)
    .WaitFor(paymentsQueue)
    .WaitFor(accountsTable)
    .WithHttpHealthCheck("/healthz");

builder.AddProject<Projects.Processing>("processing")
    .WithReference(paymentsQueue)
    .WithReference(accountsTable)
    .WaitFor(paymentsQueue)
    .WaitFor(accountsTable)
    .WithHttpHealthCheck("/healthz");

builder.Build().Run();
