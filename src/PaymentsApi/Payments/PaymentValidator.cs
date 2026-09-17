using System.Security.Claims;
using Models.Payments;
using Models.ResultPattern;

namespace PaymentsApi.Payments;

public class PaymentValidator(IHandlePayments? inner) : IHandlePayments
{
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
        if (string.IsNullOrWhiteSpace(payment.Description))
        {
            return Task.FromResult(ServiceResult<Payment>.Invalid("The Description field is required."));
        }
        if (payment.Description.Length > 30)
        {
            return Task.FromResult(ServiceResult<Payment>.Invalid(
                "The field Description must be a string or array type with a maximum length of '30'."));
        }
        if (payment.FromAccountId == payment.ToAccountId)
        {
            return Task.FromResult(ServiceResult<Payment>.Invalid("From and To account IDs must be different."));
        }
        if (payment.Amount <= 0)
        {
            return Task.FromResult(ServiceResult<Payment>.Invalid("Deposit amount must be greater than zero."));
        }

        if (inner == null)
        {
            return Task.FromResult(ServiceResult<Payment>.Ok(payment));
        }
        
        return inner.Handle(payment, user, cancellationToken);
    }
}