using Models.Payments;

namespace Processing.Payments;

public class HighAmountPolicy(IHandlePayments? inner) : IHandlePayments
{
    public Task Handle(Payment payment, string userId, CancellationToken cancellationToken = default)
    {
        if (payment.Amount > 10_000)
        {
            // TODO: emit an event or log the high deposit amount to OpenTelemetry
            // For demonstration, we will just log to the console
            Console.WriteLine($"High deposit amount detected: {payment.Amount} for user {userId}");
        }

        if (inner == null)
        {
            return Task.CompletedTask;
        }
        return inner.Handle(payment, userId, cancellationToken);
    }
}