using Models.Payments;

namespace Processing.Payments;

public class HighAmountPolicy(IHandlePayments? inner, ILogger<HighAmountPolicy> logger) : IHandlePayments
{
    public Task Handle(Payment payment, string userId, CancellationToken cancellationToken = default)
    {
        if (payment.Amount > 10_000)
        {
            logger.LogWarning(new EventId(1002, "HighAmountPayment"),
                "Payment exceeded the high-amount review threshold");
        }

        if (inner == null)
        {
            return Task.CompletedTask;
        }
        return inner.Handle(payment, userId, cancellationToken);
    }
}