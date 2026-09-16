// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using TextCopy;

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

await client.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
{
    Console.WriteLine(deviceCodeResult.Message);
    return Task.CompletedTask;
}).ExecuteAsync();

var accounts = await client.GetAccountsAsync();

while (true)
{
    Console.WriteLine();
    Console.WriteLine("Which API would you like to access?");
    Console.WriteLine("1. Payments API");
    Console.WriteLine("2. Accounts API");
    Console.WriteLine("q. Quit");

    var choice = Console.ReadLine()?.Trim();
    if (choice is null || choice.Equals("q", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Goodbye.");
        break;
    }

    string[] requestedScopes;
    switch (choice)
    {
        case "1":
            Console.WriteLine("Accessing Payments API...");
            requestedScopes = ["api://minibank-payments-api/Payment.Create"];
            break;
        case "2":
            Console.WriteLine("Accessing Accounts API...");
            requestedScopes = ["api://minibank-accounts-api/Accounts.Read"];
            break;
        default:
            Console.WriteLine("Invalid choice. Please select 1, 2, or q.");
            continue;
    }

    AuthenticationResult apiResult;
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

    Console.WriteLine($"Your API Access Token with scopes {string.Join(", ", requestedScopes)}:");
    Console.WriteLine(apiResult.AccessToken);
    await CopyToClipboardAsync(apiResult.AccessToken);
}

// Copies text to the OS clipboard cross-platform (Windows/macOS/Linux) via TextCopy, avoiding
// manual copy/paste from a wrapped console line (a common source of Base64Url corruption for long JWTs).
static async Task CopyToClipboardAsync(string text)
{
    try
    {
        await ClipboardService.SetTextAsync(text);
        Console.WriteLine("(Token copied to clipboard.)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"(Could not copy token to clipboard: {ex.Message})");
    }
}