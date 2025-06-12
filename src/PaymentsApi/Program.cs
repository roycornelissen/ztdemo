using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.Resource;
using Models.Payments;
using Models.ResultPattern;
using PaymentsApi.Accounts;
using PaymentsApi.Payments;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.WebHost.UseKestrelHttpsConfiguration();

builder.Services.AddScoped<IAccountsRepository, AccountsRepository>();

builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
builder.Services.AddSingleton<ITokenCredentialProvider, TokenCredentialProvider>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(options =>
    {
        options.TokenValidationParameters.ValidAudience = builder.Configuration.GetValue<string>("Entra:Audience");
    }, entra =>
    {
        builder.Configuration.Bind("Entra", entra);
    });

builder.Services.AddAuthorization(options =>
{
    // options.FallbackPolicy = new AuthorizationPolicyBuilder()
    //     .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
    //     .RequireAuthenticatedUser()
    //     .Build();
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddScoped<IHandlePayments, PaymentHandler>();
builder.Services.Decorate<IHandlePayments, PaymentValidator>();
builder.Services.Decorate<IHandlePayments, AccountValidator>();

builder.Services.AddSingleton(provider =>
    QueueClientFactory.CreateQueueClient(
        provider.GetRequiredService<ITokenCredentialProvider>(),
        builder.Environment,
        builder.Configuration, 
        "payments")
);

builder.Services.AddKeyedSingleton("accounts", (provider, _) =>
    TableClientFactory.CreateTableClient(
        provider.GetRequiredService<ITokenCredentialProvider>(),
        builder.Environment,
        builder.Configuration, 
        "accounts")
);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var scopeRequiredByApi = app.Configuration["Entra:Scopes"] ?? "";

app.MapGet("/test-endpoint",
    () => Results.Ok("Oops, this is an unauthenticated test endpoint!"));

app.MapPost("/payment",
        async ([FromBody] Payment payment, IHandlePayments handler, HttpContext context,
            CancellationToken cancellation) =>
        {
            context.VerifyUserHasAnyAcceptedScope(scopeRequiredByApi);

            var result = await handler.Handle(payment, context.User, cancellation);
            return result.IsSuccess 
                ? Results.Accepted() 
                : result.Error.ToApiResult();
        })
    .RequireAuthorization();

app.Run();

internal static class ErrorResponseExtensions
{
    public static IResult ToApiResult(this ErrorResponse response)
    {
        return response.ErrorType switch 
        {
            ErrorType.NotFound => Results.NotFound(response.Message),
            ErrorType.Invalid => Results.BadRequest(response.Message),
            ErrorType.Unauthorized => Results.Unauthorized(),
            ErrorType.Forbidden => Results.Forbid(),
            ErrorType.Conflict => Results.Conflict(response.Message),
            _ => Results.Problem("Unexpected error", statusCode: 500)
        };
    }
}

[JsonSerializable(typeof(Payment))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(PaymentAcceptedEvent))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}