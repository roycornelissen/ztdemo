using AccountsApi.Accounts;
using Azure.Core;
using Azure.Identity;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.Resource;
using Microsoft.IdentityModel.Logging;
using Microsoft.OpenApi;
using Models.Accounts;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateSlimBuilder(args);

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

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(options =>
    {
        options.TokenValidationParameters.NameClaimType = "preferred_username";
        options.TokenValidationParameters.ValidAudiences = validAudiences;
    }, entra =>
    {
        builder.Configuration.Bind("Entra", entra);
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddOpenApi(options =>
{
    // Declares the Entra ID authorization-code (+ PKCE) flow so Swagger UI's "Authorize"
    // dialog can drive an interactive login and attach the resulting access token to
    // "Try it out" requests. See UseSwaggerUI below for the client-side (SPA) wiring.
    options.AddDocumentTransformer((document, _, _) =>
    {
        const string scope = "api://minibank-accounts-api/Accounts.Read";
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
                        [scope] = "Read accounts"
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
builder.Services.AddHealthChecks();

builder.Services.AddScoped<AccountsRepository>();

builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
builder.Services.AddSingleton<ITokenCredentialProvider, TokenCredentialProvider>();

builder.Services.AddKeyedSingleton("accounts", (provider, _) =>
    TableClientFactory.CreateTableClient(
        provider.GetRequiredService<ITokenCredentialProvider>(),
        builder.Environment,
        builder.Configuration, 
        "accounts")
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Accounts API v1");
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

    // Swagger UI's OAuth2 middleware isn't a routed endpoint, so it runs behind the
    // fallback authorization policy below unless authentication/authorization are
    // pinned here, ahead of the endpoints that require them.
}

app.UseAuthentication();
app.UseAuthorization();

var scopeRequiredByApi = app.Configuration["Entra:Scopes"] ?? "";

app.MapGet("/accounts", async (HttpContext httpContext, AccountsRepository accountsRepository) =>
    {
        httpContext.VerifyUserHasAnyAcceptedScope(scopeRequiredByApi);

        var accounts = accountsRepository.GetAccounts(httpContext.User.Identity?.Name ?? "anonymous", httpContext.RequestAborted);

        var result = await accounts.ToArrayAsync(httpContext.RequestAborted);

        // output validation, in case we don't fully trust the data source
        if (Array.Exists(result, a => a.UserId != httpContext.User.Identity?.Name))
        {
            return Results.Forbid();
        }
        return Results.Ok(result);
    })
    .WithName("GetAccounts")
    .RequireAuthorization();

app.MapGet("/accounts/{id:int}", async (HttpContext httpContext, AccountsRepository accountsRepository, [Range(1, uint.MaxValue)] uint id) =>
{
    httpContext.VerifyUserHasAnyAcceptedScope(scopeRequiredByApi);

    var account = await accountsRepository.GetAccount(id, httpContext.RequestAborted);

    if (account == null)
    {
        return Results.NotFound();
    }

    // output validation, in case we don't fully trust the data source
    if (account.UserId != httpContext.User.Identity?.Name)
    {
        return Results.Forbid();
    }
    return Results.Ok(account);
})
    .WithName("GetAccount")
    .RequireAuthorization();

app.MapDefaultEndpoints();
app.MapHealthChecks("/healthz").AllowAnonymous();

app.Run();

[JsonSerializable(typeof(Account))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}