using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using Infrastructure;
using Processing.ExchangeRates;
using Processing.Payments;
using Processing.Transactions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
builder.Services.AddSingleton<ITokenCredentialProvider, TokenCredentialProvider>();

builder.Services.AddHttpClient();

builder.Services.AddSingleton<IExchangeRateService, ExchangeRateService>();
builder.Services.AddSingleton<TransactionRepository>();

builder.Services.AddSingleton<IHandlePayments, PaymentHandler>();
builder.Services.Decorate<IHandlePayments, HighAmountPolicy>();
builder.Services.Decorate<IHandlePayments, OnlyEuroPolicy>();

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

app.Run();
