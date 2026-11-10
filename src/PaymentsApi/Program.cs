using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Logging;
using Microsoft.OpenApi;
using Models.Payments;
using Models.ResultPattern;
using PaymentsApi.Accounts;
using PaymentsApi.Payments;

//DEMO: Use WebApplication.CreateSlimBuilder(args) to create a minimal builder without the default services and middleware. This is useful for microservices that don't need MVC, Razor Pages, or other features. However, it also means you have to add any required services and middleware manually.
var builder = WebApplication.CreateBuilder(args);
//var builder = WebApplication.CreateSlimBuilder(args);

if (builder.Environment.IsDevelopment())
{
    IdentityModelEventSource.ShowPII = true;
    IdentityModelEventSource.LogCompleteSecurityArtifact = true;
}

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
var requiredScope = builder.Configuration["Entra:Scopes"]
    ?? throw new InvalidOperationException("Entra:Scopes must be configured.");

builder.Services.AddHealthChecks();

builder.Services.AddScoped<IAccountsRepository, AccountsRepository>();

// DEMO: Replace DefaultAzureCredential with trimmed down credential chain for local development and production environments.
builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());

//builder.Services.AddSingleton<TokenCredential>(builder.CreateAzureCredential());

builder.Services.AddSingleton<ITokenCredentialProvider, TokenCredentialProvider>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "preferred_username";
        options.TokenValidationParameters.ValidAudiences = validAudiences;
    });

builder.Services.AddDelegatedScopeAuthorization("Payment.Create", requiredScope);

builder.Services.AddOpenApi(options =>
{
    // Declares the Entra ID authorization-code (+ PKCE) flow so Swagger UI's "Authorize"
    // dialog can drive an interactive login and attach the resulting access token to
    // "Try it out" requests. See UseSwaggerUI below for the client-side (SPA) wiring.
    options.AddDocumentTransformer((document, _, _) =>
    {
        const string scope = "api://minibank-payments-api/Payment.Create";
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
                        [scope] = "Create payments"
                    }
                }
            }
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("EntraID", document)] = [scope]
        });

        return Task.CompletedTask;
    });
});

builder.Services.AddScoped<PaymentHandler>();

// Validate the contract before querying accounts or sending a queue message.
builder.Services.AddScoped<IHandlePayments>(provider =>
    new PaymentValidator(
        new AccountValidator(
            provider.GetRequiredService<PaymentHandler>(),
            provider.GetRequiredService<IAccountsRepository>())));

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
            // Pre-check the API's own delegated scope in the Authorize dialog so the
            // requested access token includes it without the user manually ticking a box.
            options.OAuthScopes($"{app.Configuration["Entra:Audience"]}/{app.Configuration["Entra:Scopes"]}");
        }
    });
}

app.MapDefaultEndpoints();
app.MapHealthChecks("/healthz").AllowAnonymous().DisableRateLimiting();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Intentionally no endpoint policy: the fallback still requires the delegated scope.
app.MapGet("/test-endpoint",
    () => Results.Ok("The fallback policy protects this endpoint."));

app.MapPost("/payment",
        async ([FromBody] Payment payment, IHandlePayments handler, HttpContext context,
            CancellationToken cancellation) =>
        {
            var result = await handler.Handle(payment, context.User, cancellation);
            return result.IsSuccess 
                ? Results.Accepted() 
                : result.Error.ToApiResult();
        })
    .RequireAuthorization("Payment.Create");

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

public partial class Program;