using System.Text.Json.Serialization;
using Azure.Core;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Logging;
using Microsoft.OpenApi;
using Models.Accounts;
using Models.Payments;
using Models.ResultPattern;
using PaymentsApi;
using PaymentsApi.Accounts;
using PaymentsApi.Payments;
using PaymentsApi.Security;

//DEMO: Use WebApplication.CreateSlimBuilder(args) to create a minimal builder without the default services and middleware. This is useful for microservices that don't need MVC, Razor Pages, or other features. However, it also means you have to add any required services and middleware manually.

var builder = WebApplication.CreateBuilder(args);
//var builder = WebApplication.CreateSlimBuilder(args);

IdentityModelEventSource.ShowPII = builder.Environment.IsDevelopment();

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.WebHost.UseKestrelHttpsConfiguration();

builder.AddServiceDefaults();
builder.AddApiRequestBudgets();

var validAudiences = new[]
{
    builder.Configuration["Entra:Audience"],
    builder.Configuration["Entra:ClientId"],
    string.IsNullOrWhiteSpace(builder.Configuration["Entra:ClientId"])
        ? null
        : $"api://{builder.Configuration["Entra:ClientId"]}"
}.Where(audience => !string.IsNullOrWhiteSpace(audience))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

var authority = builder.Configuration["Entra:Authority"]
    ?? throw new InvalidOperationException("Entra:Authority must be configured.");
var readScope = builder.Configuration["Entra:Scopes"]
    ?? throw new InvalidOperationException("Entra:Scopes must be configured.");
var writeScope = builder.Configuration["Entra:WriteScope"]
    ?? throw new InvalidOperationException("Entra:WriteScope must be configured.");
var readOAuthScope = $"{builder.Configuration["Entra:Audience"]}/{readScope}";
var writeOAuthScope = $"{builder.Configuration["Entra:Audience"]}/{writeScope}";
const string submitPaymentPolicy = "SubmitPayment";

builder.Services.AddValidation();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

builder.Services.AddScoped<IAccountsRepository, AccountsRepository>();
builder.Services.AddScoped<PaymentHistoryRepository>();

var missingManagedIdentity = !builder.Environment.IsDevelopment()
    && string.IsNullOrWhiteSpace(builder.Configuration["AZURE_CLIENT_ID"]);
if (missingManagedIdentity)
{
    builder.Services.AddSingleton<TokenCredential>(_ => builder.CreateAzureCredential());
}
else
{
    builder.Services.AddSingleton<TokenCredential>(builder.CreateAzureCredential());
}

builder.Services.AddSingleton<ITokenCredentialProvider, TokenCredentialProvider>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.IncludeErrorDetails = builder.Environment.IsDevelopment();
        options.Authority = authority;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "preferred_username";
        options.TokenValidationParameters.ValidAudiences = validAudiences;
    });

builder.Services.AddDelegatedScopeAuthorization(readScope);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(submitPaymentPolicy, policy => policy
        .RequireAuthenticatedUser()
        .RequireScope(writeScope));

builder.Services.AddOpenApi(options =>
{
    // Declares the Entra ID authorization-code (+ PKCE) flow so Swagger UI's "Authorize"
    // dialog can drive an interactive login and attach the resulting access token to
    // "Try it out" requests. See UseSwaggerUI below for the client-side (SPA) wiring.
    options.AddDocumentTransformer((document, _, _) =>
    {
        var tenantId = builder.Configuration["Entra:TenantId"]
            ?? throw new InvalidOperationException("Entra:TenantId must be configured to generate the OpenAPI document.");

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["EntraID"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri($"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize"),
                    TokenUrl = new Uri($"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token"),
                    Scopes = new Dictionary<string, string>
                    {
                        [readOAuthScope] = "Read payments",
                        [writeOAuthScope] = "Submit payments"
                    }
                }
            }
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("EntraID", document)] = [readOAuthScope]
        });

        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, _) =>
    {
        if (context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .Any(metadata => metadata.Policy == submitPaymentPolicy))
        {
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("EntraID", context.Document)] = [writeOAuthScope]
                }
            ];
        }

        return Task.CompletedTask;
    });
});

builder.Services.AddScoped<PaymentHandler>();

// Validate the contract before querying accounts or sending a queue message.
builder.Services.AddScoped<IHandlePayments>(provider =>
    new PaymentValidator(
        new AccountValidator(
            provider.GetRequiredService<PaymentHandler>(),
            provider.GetRequiredService<IAccountsRepository>(),
            provider.GetRequiredService<ILogger<AccountValidator>>())));

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

builder.Services.AddKeyedSingleton("transactions", (provider, _) =>
    TableClientFactory.CreateTableClient(
        provider.GetRequiredService<ITokenCredentialProvider>(),
        builder.Environment,
        builder.Configuration,
        "transactions"));

var app = builder.Build();

if (missingManagedIdentity)
{
    app.Logger.LogWarning("AZURE_CLIENT_ID is missing. The application can start, but Azure storage operations will fail until a user-assigned managed identity is configured.");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Payments API v1");
        options.RoutePrefix = "swagger";

        // "minibank-client-minibank" is a public SPA app registration (PKCE, no secret)
        // whose redirect URIs include this API's https://.../swagger/oauth2-redirect.html.
        var swaggerUiClientId = app.Configuration["SwaggerUi:ClientId"];
        if (!string.IsNullOrWhiteSpace(swaggerUiClientId))
        {
            options.OAuthClientId(swaggerUiClientId);
            options.OAuthUsePkce();
            options.OAuthScopeSeparator(" ");
            options.OAuthScopes(readOAuthScope);
        }
    });
}

app.MapDefaultEndpoints();

app.UseSecurityEventLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Intentionally no endpoint policy
app.MapGet("/test-endpoint", TestEndpoint.Handle);

app.MapGet("/accounts/{accountId}/payments", PaymentHistoryEndpoint.Handle)
    .RequireAuthorization()
    .AddEndpointFilter<BindingFailureSecurityFilter>();

app.MapPost("/payment", SubmitPaymentEndpoint.Handle)
    .RequireAuthorization(submitPaymentPolicy)
    .AddEndpointFilter<BindingFailureSecurityFilter>();

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
[JsonSerializable(typeof(CreatePaymentRequest))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(PaymentAcceptedEvent))]
[JsonSerializable(typeof(AccountOwner))]
[JsonSerializable(typeof(IAsyncEnumerable<PaymentHistoryEntry>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}

public partial class Program;