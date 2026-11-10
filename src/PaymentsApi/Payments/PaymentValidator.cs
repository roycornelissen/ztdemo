using System.Security.Claims;
using Models.Payments;
using Models.ResultPattern;

namespace PaymentsApi.Payments;

public class PaymentValidator(IHandlePayments? inner) : IHandlePayments
{
    public const decimal MaximumAmount = 1_000_000m;

    public Task<ServiceResult<Payment>> Handle(Payment payment, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payment.Currency))
        {
            return Task.FromResult(ServiceResult<Payment>.Invalid("The Currency field is required."));
        }
        if (payment.Currency.Length is < 3 or > 3)
        {
            return Task.FromResult(ServiceResult<Payment>.Invalid(
                "The field Currency must be a string or array type with a minimum and maximum length of '3'."));
        }
        if (payment.Currency is not ("EUR" or "USD" or "GBP"))
        {
            return Task.FromResult(ServiceResult<Payment>.Invalid("Currency must be EUR, USD or GBP."));
        }
        if (string.IsNullOrWhiteSpace(payment.Description))
        {
            return Task.FromResult(ServiceResult<Payment>.Invalid("The Description field is required."));
        }
        if (payment.Description.Length > 30)
        {
            return Task.FromResult(ServiceResult<Payment>.Invalid(
                "The field Description must be a string or array type with a maximum length of '30'."));
        }
        if (payment.Description.Any(char.IsControl))
        {
            return Task.FromResult(ServiceResult<Payment>.Invalid("Description must not contain control characters."));
        }
        // The ledger stores account IDs as signed 32-bit integers.
        if (payment.FromAccountId is 0 or > int.MaxValue || payment.ToAccountId is 0 or > int.MaxValue)
        {
            return Task.FromResult(ServiceResult<Payment>.Invalid("Account IDs must be between 1 and 2147483647."));
        }
        if (payment.FromAccountId == payment.ToAccountId)
        {
            return Task.FromResult(ServiceResult<Payment>.Invalid("From and To account IDs must be different."));
        }
        if (payment.Amount <= 0)
        {
            return Task.FromResult(ServiceResult<Payment>.Invalid("Deposit amount must be greater than zero."));
        }
        if (payment.Amount > MaximumAmount || decimal.Round(payment.Amount, 2) != payment.Amount)
        {
            return Task.FromResult(ServiceResult<Payment>.Invalid(
                "Payment amount must not exceed 1000000 and must have at most two decimal places."));
        }

        if (inner == null)
        {
            return Task.FromResult(ServiceResult<Payment>.Ok(payment));
        }
        
        return inner.Handle(payment, user, cancellationToken);
    }
}