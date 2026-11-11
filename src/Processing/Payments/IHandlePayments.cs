using Models.Accounts;
using Models.Payments;

namespace Processing.Payments;

public interface IHandlePayments
{
    Task Handle(
        Payment payment,
        AccountOwner owner,
        CancellationToken cancellationToken = default);
}