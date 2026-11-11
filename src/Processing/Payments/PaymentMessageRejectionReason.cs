namespace Processing.Payments;

public enum PaymentMessageRejectionReason
{
    MalformedJson,
    InvalidEnvelope,
    InvalidOwner,
    InvalidPayment,
    SourceAccountNotFound,
    SourceAccountOwnershipUnavailable,
    SourceAccountOwnershipMismatch,
    DestinationAccountNotFound
}
