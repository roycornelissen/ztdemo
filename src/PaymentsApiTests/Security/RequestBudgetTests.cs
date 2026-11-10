using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Models.Payments;
using NSubstitute;
using PaymentsApi.Payments;
using Shouldly;

namespace PaymentsApiTests.Security;

public class RequestBudgetTests
{
    [Test]
    public async Task The_first_request_over_budget_gets_429_but_liveness_still_works()
    {
        using var factory = new PaymentsApiFactory();
        using var client = factory.CreateClient();
        for (var i = 0; i < ApiRequestBudgetExtensions.RequestsPerMinute; i++)
        {
            using var allowed = await client.GetAsync("/test-endpoint");
            allowed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var rejected = await client.GetAsync("/test-endpoint");
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter!.Delta!.Value.ShouldBeGreaterThan(TimeSpan.Zero);
        rejected.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");

        using var health = await client.GetAsync("/healthz");
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestCase(4096, false, HttpStatusCode.Accepted)]
    [TestCase(4097, false, HttpStatusCode.RequestEntityTooLarge)]
    [TestCase(4096, true, HttpStatusCode.Accepted)]
    [TestCase(4097, true, HttpStatusCode.RequestEntityTooLarge)]
    public async Task Kestrel_enforces_the_exact_byte_limit_even_without_content_length(
        int size, bool chunked, HttpStatusCode expected)
    {
        var handler = Substitute.For<IHandlePayments>();
        handler.Handle(Arg.Any<Payment>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(info => info.Arg<Payment>());
        using var factory = new PaymentsApiFactory(services => services.AddSingleton(handler));
        factory.UseKestrel(0);
        using var client = factory.CreateClient();
        var json = """{"fromAccountId":1,"toAccountId":2,"amount":100,"description":"Demo"}""";
        var body = Encoding.UTF8.GetBytes(json.PadRight(size));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/payment");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PaymentsApiFactory.CreateToken());
        request.Content = chunked
            ? new ChunkedContent(body)
            : new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(expected);
        response.Headers.Server.ShouldBeEmpty();
        if (expected == HttpStatusCode.RequestEntityTooLarge)
        {
            await handler.DidNotReceiveWithAnyArgs().Handle(default!, default!, default);
        }
    }

    [TestCase("""{"fromAccountId":-1,"toAccountId":2,"amount":100}""")]
    [TestCase("""{"fromAccountId":1,"toAccountId":2,"amount":100,"userId":"forged"}""")]
    public async Task Invalid_json_contracts_return_400_without_calling_the_handler(string json)
    {
        var handler = Substitute.For<IHandlePayments>();
        using var factory = new PaymentsApiFactory(services => services.AddSingleton(handler));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PaymentsApiFactory.CreateToken());

        using var response = await client.PostAsync("/payment", new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await handler.DidNotReceiveWithAnyArgs().Handle(default!, default!, default);
    }

    private sealed class ChunkedContent(byte[] body) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(body).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
