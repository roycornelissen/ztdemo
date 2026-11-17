using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AccountsApi.Accounts;
using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Logging;
using NSubstitute;
using Shouldly;

namespace PaymentsApiTests.Security;

[NonParallelizable]
public class AccountsAuthorizationTests
{
    [TestCase("Accounts.Read")]
    [TestCase("Accounts.Write Accounts.Read")]
    public async Task Read_scope_and_owner_claims_allow_listing_owned_accounts(string scope)
    {
        using var factory = new AccountsApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PaymentsApiFactory.CreateToken(scope, includeOwner: true));

        using var response = await client.GetAsync("/accounts");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetArrayLength().ShouldBe(1);
        body.RootElement[0].GetProperty("id").GetUInt32().ShouldBe(1u);
    }

    [TestCase("Accounts.Read.Extra", true)]
    [TestCase("accounts.read", true)]
    [TestCase("Payment.Read", true)]
    [TestCase("Accounts.Read", false)]
    public async Task Listing_still_requires_exact_scope_and_owner_claims(string scope, bool includeOwner)
    {
        using var factory = new AccountsApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PaymentsApiFactory.CreateToken(scope, includeOwner: includeOwner));

        using var response = await client.GetAsync("/accounts");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestCase("Development", true)]
    [TestCase("Staging", false)]
    [TestCase("Production", false)]
    public async Task Identity_web_diagnostics_follow_the_api_environment(string environment, bool expected)
    {
        using var factory = new AccountsApiFactory(environment);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PaymentsApiFactory.CreateToken("Accounts.Read", expired: true));

        using var response = await client.GetAsync("/accounts");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var challenge = response.Headers.WwwAuthenticate.Single();
        if (expected)
        {
            challenge.Parameter.ShouldNotBeNull();
            challenge.Parameter.ShouldContain("expired");
        }
        else
        {
            challenge.Parameter.ShouldBeNull();
        }
        IdentityModelEventSource.ShowPII.ShouldBe(expected);
        IdentityModelEventSource.LogCompleteSecurityArtifact.ShouldBeFalse();
    }

    [TestCase("Development", true)]
    [TestCase("Production", false)]
    public async Task Scope_failure_details_are_logged_only_in_development(string environment, bool expected)
    {
        var logs = new SecurityEventTests.RecordingLoggerProvider();
        using var factory = new AccountsApiFactory(environment,
            services => services.AddLogging(logging => logging.AddProvider(logs)));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PaymentsApiFactory.CreateToken("Payment.Read", includeOwner: true));

        using var response = await client.GetAsync("/accounts");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        logs.Entries.Any(entry =>
            entry.Category.StartsWith("Microsoft.AspNetCore.Authorization", StringComparison.Ordinal) &&
            entry.Message.Contains("ScopeAuthorizationRequirement", StringComparison.Ordinal))
            .ShouldBe(expected);
    }

    private sealed class AccountsApiFactory(
        string environment = "Development", Action<IServiceCollection>? configureServices = null)
        : WebApplicationFactory<AccountsRepository>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("AZURE_CLIENT_ID", "00000000-0000-0000-0000-000000000003");
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                    PaymentsApiFactory.ConfigureOfflineValidation);

                var table = Substitute.For<TableClient>();
                var row = new AccountEntity
                {
                    PartitionKey = "accounts",
                    RowKey = "1",
                    Description = "Everyday account",
                    OwnerTenantId = PaymentsApiFactory.TenantId.ToString("D"),
                    OwnerObjectId = PaymentsApiFactory.ObjectId.ToString("D")
                };
                table.QueryAsync(
                        Arg.Any<Expression<Func<AccountEntity, bool>>>(),
                        cancellationToken: Arg.Any<CancellationToken>())
                    .Returns(AsyncPageable<AccountEntity>.FromPages([
                        Page<AccountEntity>.FromValues([row], null, Substitute.For<Response>())
                    ]));
                services.AddKeyedSingleton("accounts", table);
                configureServices?.Invoke(services);
            });
        }
    }
}
