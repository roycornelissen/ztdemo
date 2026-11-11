using Models.Payments;

namespace Processing.Payments;

public sealed class PaymentMessageValidator(IAccountOwnershipReader accounts)
{
    public async Task<PaymentMessageRejectionReason?> ValidateAsync(
        PaymentAcceptedEvent? paymentEvent,
        CancellationToken cancellationToken = default)
    {
        if (paymentEvent is null || paymentEvent.Payment is null)
        {
            return PaymentMessageRejectionReason.InvalidEnvelope;
        }

        if (paymentEvent.Owner is null ||
            paymentEvent.Owner.TenantId == Guid.Empty ||
            paymentEvent.Owner.ObjectId == Guid.Empty)
        {
            return PaymentMessageRejectionReason.InvalidOwner;
        }

        if (PaymentContractValidator.Validate(paymentEvent.Payment) is not null)
        {
            return PaymentMessageRejectionReason.InvalidPayment;
        }

        var sourceAccount = await accounts.GetAsync(
            paymentEvent.Payment.FromAccountId,
            cancellationToken);
        if (sourceAccount is null)
        {
            return PaymentMessageRejectionReason.SourceAccountNotFound;
        }
        if (sourceAccount.Owner is null)
        {
            return PaymentMessageRejectionReason.SourceAccountOwnershipUnavailable;
        }
        if (sourceAccount.Owner != paymentEvent.Owner)
        {
            return PaymentMessageRejectionReason.SourceAccountOwnershipMismatch;
        }

        var destinationAccount = await accounts.GetAsync(
            paymentEvent.Payment.ToAccountId,
            cancellationToken);
        if (destinationAccount is null)
        {
            return PaymentMessageRejectionReason.DestinationAccountNotFound;
        }

        return null;
    }
}
