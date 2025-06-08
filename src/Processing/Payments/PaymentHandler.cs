using Models.Payments;
using Processing.Transactions;

namespace Processing.Payments;

public class PaymentHandler(TransactionRepository repository) : IHandlePayments
{
    public async Task Handle(Payment payment, string userId, CancellationToken cancellationToken = default)
    {
        BankTransaction[] transactions =
        [
            new()
            {
                AccountId = Convert.ToInt32(payment.FromAccountId), Amount = -(payment.Amount),
                Description = payment.Description
            },
            new()
            {
                AccountId = Convert.ToInt32(payment.ToAccountId), Amount = payment.Amount,
                Description = payment.Description
            }
        ];

        await repository.StoreTransactions(transactions, cancellationToken);
    }
}