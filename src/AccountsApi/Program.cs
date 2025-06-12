using System.Text.Json.Serialization;
using AccountsApi.Accounts;
using Azure.Core;
using Azure.Identity;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.Resource;
using Models.Accounts;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.WebHost.UseKestrelHttpsConfiguration();

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
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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
    app.MapOpenApi();
}

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
    .WithOpenApi()
    .RequireAuthorization();

app.Run();

[JsonSerializable(typeof(Account))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}