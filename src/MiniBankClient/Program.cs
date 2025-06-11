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

string[] requestedScopes = []; 

AuthenticationResult apiResult;
var accounts = await client.GetAccountsAsync();

Console.WriteLine("Which API would you like to access?");
Console.WriteLine("1. Payments API");
Console.WriteLine("2. Accounts API");
var choice = Console.ReadLine();
if (choice == "1")
{
    Console.WriteLine("Accessing Payments API...");
    requestedScopes = new[] { "api://minibank-payments-api/Payment.Create" };
}
else if (choice == "2")
{
    Console.WriteLine("Accessing Accounts API...");
    requestedScopes = new[] { "api://minibank-accounts-api/Accounts.Read" };
}
else
{
    Console.WriteLine("Invalid choice. Exiting.");
    return;
}

try
{
    apiResult = await client.AcquireTokenSilent(requestedScopes, accounts.FirstOrDefault())
        .ExecuteAsync();
}
catch (MsalUiRequiredException)
{
    apiResult = await client.AcquireTokenWithDeviceCode(requestedScopes, deviceCodeResult =>
    {
        Console.WriteLine(deviceCodeResult.Message);
        return Task.CompletedTask;
    }).ExecuteAsync();
}

if (apiResult != null)
{
    Console.WriteLine($"Your API Access Token with scopes { string.Join(", ", requestedScopes) }:");
    Console.WriteLine(apiResult.AccessToken);
}
else
{
    Console.WriteLine("Failed to acquire access token.");
}