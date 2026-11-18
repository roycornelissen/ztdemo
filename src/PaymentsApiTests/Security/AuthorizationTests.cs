using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Models.Accounts;
using Models.Payments;
using NSubstitute;
using PaymentsApi.Accounts;
using PaymentsApi.Payments;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Shouldly;

namespace PaymentsApiTests.Security;

[TestFixture(false)]
[TestFixture(true)]
public class AuthorizationTests(bool mapInboundClaims)
{
    private PaymentsApiFactory factory = null!;
    private HttpClient client = null!;

    [SetUp]
    public void SetUp()
    {
        var handler = Substitute.For<IHandlePayments>();
        var accounts = Substitute.For<IAccountsRepository>();
        accounts.GetAccount(2, Arg.Any<CancellationToken>())
            .Returns(new Account { Id = 2 });
        handler.Handle(Arg.Any<Payment>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(info => info.Arg<Payment>());
        factory = new PaymentsApiFactory(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                options => options.MapInboundClaims = mapInboundClaims);
            services.AddSingleton(handler);
            services.AddSingleton(accounts);
        });
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [TearDown]
    public void TearDown()
    {
        client.Dispose();
        factory.Dispose();
    }

    [TestCase("/test-endpoint")]
    [TestCase("/payment")]
    public async Task Endpoints_require_authentication_even_without_explicit_metadata(string path)
    {
        using var request = new HttpRequestMessage(path == "/payment" ? HttpMethod.Post : HttpMethod.Get, path);
        using var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestCase(null)]
    [TestCase("Accounts.Read")]
    [TestCase("Payment.Write")]
    [TestCase("Payment.Read.Extra")]
    [TestCase("payment.read")]
    public async Task A_valid_token_is_not_sufficient_without_the_exact_scope(string? scope)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PaymentsApiFactory.CreateToken(scope));
        using var response = await client.GetAsync("/test-endpoint");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestCase("Payment.Read", ClaimConstants.Scp)]
    [TestCase("Payment.Write Payment.Read", ClaimConstants.Scp)]
    [TestCase("Payment.Read", ClaimConstants.Scope)]
    [TestCase("Accounts.Read Payment.Read", ClaimConstants.Scope)]
    public async Task An_exact_delegated_scope_grants_access(string scope, string claimType)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PaymentsApiFactory.CreateToken(scope, claimType: claimType));
        using var response = await client.GetAsync("/test-endpoint");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestCase("roles")]
    [TestCase("scope")]
    public async Task Other_claim_types_do_not_grant_delegated_access(string claimType)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PaymentsApiFactory.CreateToken(claimType: claimType));
        using var response = await client.GetAsync("/test-endpoint");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    public async Task Invalid_tokens_are_rejected(bool expired, bool wrongAudience, bool wrongSignature)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PaymentsApiFactory.CreateToken(
                audience: wrongAudience ? "another-api" : null, expired: expired, wrongSignature: wrongSignature));
        using var response = await client.GetAsync("/test-endpoint");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestCase("Payment.Read Payment.Write", HttpStatusCode.Accepted)]
    [TestCase("Accounts.Read Payment.Write Payment.Read", HttpStatusCode.Accepted)]
    [TestCase(null, HttpStatusCode.Forbidden)]
    [TestCase("Payment.Read", HttpStatusCode.Forbidden)]
    [TestCase("Payment.Write", HttpStatusCode.Accepted)]
    [TestCase("Payment.Read Payment.Create", HttpStatusCode.Forbidden)]
    [TestCase("Payment.Read Payment.Write.Extra", HttpStatusCode.Forbidden)]
    [TestCase("Payment.Read payment.write", HttpStatusCode.Forbidden)]
    public async Task Payment_submission_requires_write_but_not_read(string? scope, HttpStatusCode expected)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PaymentsApiFactory.CreateToken(scope));
        using var content = new StringContent(
            """{"fromAccountId":1,"toAccountId":2,"amount":100,"description":"Demo"}""",
            Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/payment", content);

        response.StatusCode.ShouldBe(expected);
    }

    [Test]
    public async Task OpenApi_documents_the_scopes_required_for_each_payment_operation()
    {
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        const string read = "api://minibank-payments-api/Payment.Read";
        const string write = "api://minibank-payments-api/Payment.Write";

        var defaults = root.GetProperty("security")[0].GetProperty("EntraID");
        defaults.EnumerateArray().Select(scope => scope.GetString()).ShouldBe([read]);

        var post = root.GetProperty("paths").GetProperty("/payment").GetProperty("post");
        post.GetProperty("security").GetArrayLength().ShouldBe(1);
        post.GetProperty("security")[0].GetProperty("EntraID")
            .EnumerateArray().Select(scope => scope.GetString()).ShouldBe([write]);

        var get = root.GetProperty("paths").GetProperty("/accounts/{accountId}/payments").GetProperty("get");
        get.TryGetProperty("security", out _).ShouldBeFalse();
    }

    [Test]
    public async Task Health_endpoints_are_anonymous_exceptions()
    {
        foreach (var path in new[] { "/health", "/alive" })
        {
            using var response = await client.GetAsync(path);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Test]
    public async Task Production_health_endpoints_are_available_when_enabled()
    {
        using var productionFactory = new PaymentsApiFactory(
            environment: "Production",
            exposeHealthEndpoints: true);
        using var productionClient = productionFactory.CreateClient();

        foreach (var path in new[] { "/health", "/alive" })
        {
            using var response = await productionClient.GetAsync(path);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }
}
