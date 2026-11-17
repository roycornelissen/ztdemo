using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Models.Payments;
using NSubstitute;
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
        handler.Handle(Arg.Any<Payment>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(info => info.Arg<Payment>());
        factory = new PaymentsApiFactory(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                options => options.MapInboundClaims = mapInboundClaims);
            services.AddSingleton(handler);
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
    [TestCase("Payment.Create.Extra")]
    [TestCase("payment.create")]
    public async Task A_valid_token_is_not_sufficient_without_the_exact_scope(string? scope)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PaymentsApiFactory.CreateToken(scope));
        using var response = await client.GetAsync("/test-endpoint");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestCase("Payment.Create", ClaimConstants.Scp)]
    [TestCase("Accounts.Read Payment.Create", ClaimConstants.Scp)]
    [TestCase("Payment.Create", ClaimConstants.Scope)]
    [TestCase("Accounts.Read Payment.Create", ClaimConstants.Scope)]
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

    [TestCase("Payment.Create", HttpStatusCode.Accepted)]
    [TestCase("Accounts.Read Payment.Create", HttpStatusCode.Accepted)]
    [TestCase(null, HttpStatusCode.Forbidden)]
    [TestCase("Accounts.Read", HttpStatusCode.Forbidden)]
    [TestCase("Payment.Create.Extra", HttpStatusCode.Forbidden)]
    [TestCase("payment.create", HttpStatusCode.Forbidden)]
    public async Task Payment_endpoint_enforces_its_declared_scope(string? scope, HttpStatusCode expected)
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
    public async Task Liveness_is_an_explicit_anonymous_exception()
    {
        using var response = await client.GetAsync("/healthz");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
