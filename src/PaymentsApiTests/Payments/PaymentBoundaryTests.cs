using System.Security.Claims;
using System.Text.Json;
using Models.Payments;
using Models.ResultPattern;
using NSubstitute;
using PaymentsApi.Accounts;
using PaymentsApi.Payments;
using Shouldly;

namespace PaymentsApiTests.Payments;

public class PaymentBoundaryTests
{
    private static Payment ValidPayment => new()
    {
        FromAccountId = 1,
        ToAccountId = 2,
        Amount = 100,
        Currency = "EUR",
        Description = "Demo transfer"
    };

    [TestCase(0u, 2u)]
    [TestCase(1u, 0u)]
    [TestCase(uint.MaxValue, 2u)]
    [TestCase(1u, uint.MaxValue)]
    public async Task Unsigned_does_not_replace_domain_bounds(uint from, uint to)
    {
        await AssertRejectedBeforeSideEffects(ValidPayment with { FromAccountId = from, ToAccountId = to });
    }

    [TestCase("eur")]
    [TestCase("../")]
    [TestCase("XYZ")]
    [TestCase(null)]
    public async Task Currency_is_an_explicit_allowlist(string? currency)
    {
        await AssertRejectedBeforeSideEffects(ValidPayment with { Currency = currency! });
    }

    [TestCase("1000000.01")]
    [TestCase("0.001")]
    [TestCase("79228162514264337593543950335")]
    public async Task Amount_is_bounded_and_has_currency_precision(string amount)
    {
        await AssertRejectedBeforeSideEffects(ValidPayment with
        {
            Amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)
        });
    }

    [Test]
    public async Task Description_cannot_smuggle_control_characters()
    {
        await AssertRejectedBeforeSideEffects(ValidPayment with { Description = "transfer\r\nforged log" });
    }

    [Test]
    public async Task Null_description_is_rejected()
    {
        await AssertRejectedBeforeSideEffects(ValidPayment with { Description = null! });
    }

    [TestCase("0.01")]
    [TestCase("1000000")]
    public async Task Exact_amount_and_account_boundaries_are_accepted(string amount)
    {
        var inner = Substitute.For<IHandlePayments>();
        var payment = ValidPayment with
        {
            FromAccountId = int.MaxValue,
            Amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)
        };
        inner.Handle(payment, Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns(payment);

        var result = await new PaymentValidator(inner).Handle(payment, new ClaimsPrincipal());

        result.IsSuccess.ShouldBeTrue();
        await inner.Received(1).Handle(payment, Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void Unknown_json_fields_are_not_silently_ignored()
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Payment>(
            """{"FromAccountId":1,"ToAccountId":2,"Amount":100,"UserId":"another-user"}"""));
    }

    [Test]
    public void Negative_json_account_ids_cannot_be_deserialized()
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Payment>(
            """{"FromAccountId":-1,"ToAccountId":2,"Amount":100}"""));
    }

    private static async Task AssertRejectedBeforeSideEffects(Payment payment)
    {
        var repository = Substitute.For<IAccountsRepository>();
        var sender = Substitute.For<IHandlePayments>();
        var validator = new PaymentValidator(new AccountValidator(sender, repository));

        var result = await validator.Handle(payment, new ClaimsPrincipal());

        result.IsSuccess.ShouldBeFalse();
        result.Error!.ErrorType.ShouldBe(ErrorType.Invalid);
        await repository.DidNotReceiveWithAnyArgs().GetAccount(default, default);
        await sender.DidNotReceiveWithAnyArgs().Handle(default!, default!, default);
    }
}
