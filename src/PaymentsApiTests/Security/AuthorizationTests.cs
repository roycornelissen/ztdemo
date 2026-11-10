using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace PaymentsApiTests.Security;

public class AuthorizationTests
{
    private PaymentsApiFactory factory = null!;
    private HttpClient client = null!;

    [SetUp]
    public void SetUp()
    {
        factory = new PaymentsApiFactory();
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

    [TestCase("Payment.Create", "scp")]
    [TestCase("Accounts.Read Payment.Create", "scope")]
    public async Task An_exact_delegated_scope_grants_access(string scope, string claimType)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PaymentsApiFactory.CreateToken(scope, claimType: claimType));
        using var response = await client.GetAsync("/test-endpoint");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task An_application_role_is_not_a_delegated_scope()
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PaymentsApiFactory.CreateToken(claimType: "roles"));
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

    [Test]
    public async Task Liveness_is_an_explicit_anonymous_exception()
    {
        using var response = await client.GetAsync("/healthz");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
