using Models.Payments;
using Processing.ExchangeRates;

namespace Processing.Payments;

public class OnlyEuroPolicy(IHandlePayments? inner, IExchangeRateService exchangeRateService) : IHandlePayments
{
    public async Task Handle(Payment payment, string userId, CancellationToken cancellationToken = default)
    {
        if (!payment.Currency.Equals("EUR", StringComparison.OrdinalIgnoreCase))
        {
            payment = payment with
            {
                Currency = "EUR",
                Amount = await exchangeRateService.ConvertRate(payment.Amount, payment.Currency, "EUR", cancellationToken)
            };
        }
        
        if (inner == null)
        {
            return;
        }
        await inner.Handle(payment, userId, cancellationToken);
    }
}