using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using Models.Accounts;
using NSubstitute;
using PaymentsApi.Accounts;
using Shouldly;

namespace PaymentsApiTests.Security;

public class PaymentHistoryTests
{
    private const string HistoryUrl = "/accounts/42/payments";
    private static readonly AccountOwner Owner = new(PaymentsApiFactory.TenantId, PaymentsApiFactory.ObjectId);

    [Test]
    public async Task OpenApi_describes_accountId_as_a_single_integer_type_for_Swagger()
    {
        using var factory = new PaymentsApiFactory();
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var parameter = document.RootElement.GetProperty("paths")
            .GetProperty("/accounts/{accountId}/payments").GetProperty("get")
            .GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == "accountId");

        parameter.GetProperty("in").GetString().ShouldBe("path");
        parameter.GetProperty("required").GetBoolean().ShouldBeTrue();
        var schema = parameter.GetProperty("schema");
        schema.GetProperty("type").ValueKind.ShouldBe(JsonValueKind.String);
        schema.GetProperty("type").GetString().ShouldBe("integer");
    }

    [Test]
    public async Task Read_scope_returns_only_the_requested_accounts_ledger_entries()
    {
        var accounts = Substitute.For<IAccountsRepository>();
        accounts.GetAccount(42, Arg.Any<CancellationToken>()).Returns(new Account { Id = 42, Owner = Owner });
        var ledger = Substitute.For<TableClient>();
        var response = Substitute.For<Response>();
        var rows = new TableEntity[]
        {
            new("42", "debit") { ["Amount"] = "-12.34", ["Description"] = "Outgoing" },
            new("42", "credit") { ["Amount"] = "56.789", ["Description"] = "Incoming" }
        };
        ledger.QueryAsync<TableEntity>("PartitionKey eq '42'", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(AsyncPageable<TableEntity>.FromPages([
                Page<TableEntity>.FromValues([rows[0]], "next", response),
                Page<TableEntity>.FromValues([rows[1]], null, response)
            ]));
        using var factory = CreateFactory(accounts, ledger);
        using var client = factory.CreateClient();
        Authorize(client, "Payment.Read");

        using var result = await client.GetAsync(HistoryUrl);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        body.RootElement.GetArrayLength().ShouldBe(2);
        body.RootElement[0].GetProperty("amount").GetDecimal().ShouldBe(-12.34m);
        body.RootElement[1].GetProperty("amount").GetDecimal().ShouldBe(56.789m);
        body.RootElement[1].GetProperty("description").GetString().ShouldBe("Incoming");
        body.RootElement[0].TryGetProperty("partitionKey", out _).ShouldBeFalse();
    }

    [TestCase(null, HttpStatusCode.Unauthorized)]
    [TestCase("Payment.Write", HttpStatusCode.Forbidden)]
    [TestCase("Payment.Read.Extra", HttpStatusCode.Forbidden)]
    public async Task History_rejects_tokens_without_read_scope(string? scope, HttpStatusCode expected)
    {
        var accounts = Substitute.For<IAccountsRepository>();
        using var factory = CreateFactory(accounts, Substitute.For<TableClient>());
        using var client = factory.CreateClient();
        if (scope is not null)
        {
            Authorize(client, scope);
        }

        using var result = await client.GetAsync(HistoryUrl);

        result.StatusCode.ShouldBe(expected);
        accounts.ReceivedCalls().ShouldBeEmpty();
    }

    [TestCase("missing", HttpStatusCode.NotFound)]
    [TestCase("other-tenant", HttpStatusCode.NotFound)]
    [TestCase("other-user", HttpStatusCode.NotFound)]
    [TestCase("unowned", HttpStatusCode.NotFound)]
    [TestCase("missing-claims", HttpStatusCode.Forbidden)]
    public async Task History_does_not_query_the_ledger_without_verified_ownership(string scenario, HttpStatusCode expected)
    {
        var accounts = Substitute.For<IAccountsRepository>();
        Account? account = scenario == "missing" ? null : new Account
        {
            Id = 42,
            Owner = scenario switch
            {
                "other-tenant" => Owner with { TenantId = Guid.NewGuid() },
                "other-user" => Owner with { ObjectId = Guid.NewGuid() },
                "unowned" => null,
                _ => Owner
            }
        };
        accounts.GetAccount(42, Arg.Any<CancellationToken>()).Returns(account);
        var ledger = Substitute.For<TableClient>();
        using var factory = CreateFactory(accounts, ledger);
        using var client = factory.CreateClient();
        Authorize(client, "Payment.Read", includeOwner: scenario != "missing-claims");

        using var result = await client.GetAsync(HistoryUrl);

        result.StatusCode.ShouldBe(expected);
        ledger.ReceivedCalls().ShouldBeEmpty();
    }

    private static PaymentsApiFactory CreateFactory(IAccountsRepository accounts, TableClient ledger) =>
        new(services =>
        {
            services.AddSingleton(accounts);
            services.AddKeyedSingleton("transactions", ledger);
        });

    private static void Authorize(HttpClient client, string scope, bool includeOwner = true) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PaymentsApiFactory.CreateToken(scope, includeOwner: includeOwner));
}
