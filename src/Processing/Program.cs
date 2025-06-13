using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using Infrastructure;
using Processing.ExchangeRates;
using Processing.Payments;
using Processing.Transactions;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddHealthChecks();

builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
builder.Services.AddSingleton<ITokenCredentialProvider, TokenCredentialProvider>();

builder.Services.AddHttpClient();

builder.Services.AddSingleton<IExchangeRateService, ExchangeRateService>();
builder.Services.AddSingleton<TransactionRepository>();

builder.Services.AddSingleton<IHandlePayments, PaymentHandler>();
builder.Services.Decorate<IHandlePayments>((inner, _) =>
    new HighAmountPolicy(inner)
);
builder.Services.Decorate<IHandlePayments>((inner, provider) =>
    new OnlyEuroPolicy(inner, provider.GetRequiredService<IExchangeRateService>())
);

builder.Services.AddSingleton(provider =>
    QueueClientFactory.CreateQueueClient(
        provider.GetRequiredService<ITokenCredentialProvider>(),
        builder.Environment,
        builder.Configuration, 
        "payments")
);

builder.Services.AddKeyedSingleton<TableClient>("transactions", (provider, _) =>
    TableClientFactory.CreateTableClient(
        provider.GetRequiredService<ITokenCredentialProvider>(),
        builder.Environment,
        builder.Configuration, 
        "transactions")
);

builder.Services.AddKeyedSingleton<TableClient>("accounts", (provider, _) =>
    TableClientFactory.CreateTableClient(
        provider.GetRequiredService<ITokenCredentialProvider>(),
        builder.Environment,
        builder.Configuration, 
        "accounts")
);

builder.Services.AddHostedService<PaymentMessageProcessor>();

var app = builder.Build();

app.MapHealthChecks("/healthz").AllowAnonymous();

app.Run();
