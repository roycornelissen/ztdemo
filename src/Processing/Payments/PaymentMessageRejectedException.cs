namespace Processing.Payments;

public sealed class PaymentMessageRejectedException(PaymentMessageRejectionReason reason)
    : Exception("The payment message failed worker-side validation.")
{
    public PaymentMessageRejectionReason Reason { get; } = reason;
}
