using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Models.Accounts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using PaymentsApi.Accounts;
using PaymentsApi.Payments;
using PaymentsApiTests.Security;
using Shouldly;

namespace PaymentsApiTests.Payments;

public class AsyncPaymentValidationTests
{
    [Test]
    public async Task Unknown_beneficiary_is_rejected_before_payment_handling()
    {
        var accounts = Substitute.For<IAccountsRepository>();
        accounts.GetAccount(2, Arg.Any<CancellationToken>()).Returns((Account?)null);
        var handler = Substitute.For<IHandlePayments>();

        using var factory = new PaymentsApiFactory();
        using var testFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAccountsRepository>();
            services.AddSingleton(accounts);
            services.RemoveAll<IHandlePayments>();
            services.AddSingleton(handler);
        }));
        using var client = testFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", PaymentsApiFactory.CreateToken("Payment.Write", includeOwner: true));

        using var content = new StringContent(
            """{"fromAccountId":1,"toAccountId":2,"amount":100,"description":"Demo"}""",
            Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/payment", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await accounts.Received(1).GetAccount(2, Arg.Any<CancellationToken>());
        await handler.DidNotReceiveWithAnyArgs().Handle(default!, default!, default);
    }
}
