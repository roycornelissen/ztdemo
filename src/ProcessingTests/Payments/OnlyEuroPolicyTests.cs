using Models.Payments;
using NSubstitute;
using Processing.ExchangeRates;
using Processing.Payments;
using Shouldly;

namespace ProcessingTests.Payments;

public class OnlyEuroPolicyTests
{
    [Test]
    public async Task Leaves_amount_unchanged_when_in_EUR()
    {
        var innerHandler = Substitute.For<IHandlePayments>();
        
        var exchangeRateService = Substitute.For<IExchangeRateService>();
        exchangeRateService
            .ConvertRate(Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(x => x.ArgAt<decimal>(0) * 2);
        
        // Arrange
        var policy = new OnlyEuroPolicy(innerHandler, exchangeRateService);
        var deposit = new Payment
        {
            Currency = "EUR",
            Description = "Test deposit",
            Amount = 100,
            FromAccountId = 1,
            ToAccountId = 2
        };
        
        // Act
        await policy.Handle(deposit, null);

        // Assert
        await exchangeRateService.DidNotReceive().ConvertRate(Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        
        await innerHandler.Received(1).Handle(deposit, null, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Converts_other_currencies_to_EUR()
    {
        var innerHandler = Substitute.For<IHandlePayments>();
        Payment? convertedDeposit = null;
        innerHandler
            .When(x => x.Handle(Arg.Any<Payment>(), Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(x => convertedDeposit = x.ArgAt<Payment>(0));

        var exchangeRateService = Substitute.For<IExchangeRateService>();
        exchangeRateService
            .ConvertRate(Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(x => x.ArgAt<decimal>(0) * 2);
        
        // Arrange
        var policy = new OnlyEuroPolicy(innerHandler, exchangeRateService);
        var deposit = new Payment
        {
            Currency = "GBP",
            Description = "Test deposit",
            Amount = 100,
            FromAccountId = 1,
            ToAccountId = 2
        };
        
        // Act
        await policy.Handle(deposit, null);

        // Assert
        await exchangeRateService.Received(1).ConvertRate(100, "GBP", "EUR", Arg.Any<CancellationToken>());

        convertedDeposit!.Amount.ShouldBe(200);
        convertedDeposit!.Currency.ShouldBe("EUR");
    }
}