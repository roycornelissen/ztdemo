using Models.Payments;

namespace Processing.Payments;

public interface IHandlePayments
{
    Task Handle(
        Payment payment,
        string userId,
        CancellationToken cancellationToken = default);
}