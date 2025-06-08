// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;

Console.WriteLine("MiniBank Client");

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .Build();

//read the Entra:ClientId from the configuration 
var clientId = config["Entra:ClientId"];
var tenantId = config["Entra:TenantId"];
var clientSecret = config["Entra:ClientSecret"];

var client = PublicClientApplicationBuilder
    .Create(clientId)
    .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
    .WithDefaultRedirectUri()
    .Build();

var scopes = new[] { "user.read" };

var result = await client.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
{
    Console.WriteLine(deviceCodeResult.Message);
    return Task.CompletedTask;
}).ExecuteAsync();

var apiScopes = new[] { "api://minibank-payments-api/Payment.Create" };
AuthenticationResult apiResult;
var accounts = await client.GetAccountsAsync();

try
{
    apiResult = await client.AcquireTokenSilent(apiScopes, accounts.FirstOrDefault())
        .ExecuteAsync();
}
catch (MsalUiRequiredException)
{
    apiResult = await client.AcquireTokenWithDeviceCode(apiScopes, deviceCodeResult =>
    {
        Console.WriteLine(deviceCodeResult.Message);
        return Task.CompletedTask;
    }).ExecuteAsync();
}

if (apiResult != null)
{
    Console.WriteLine($"Payments API Access Token: {apiResult.AccessToken}");
}
else
{
    Console.WriteLine("Failed to acquire access token.");
}