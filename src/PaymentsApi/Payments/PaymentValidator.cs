using System.Security.Claims;
using Models.Payments;
using Models.ResultPattern;

namespace PaymentsApi.Payments;

public class PaymentValidator(IHandlePayments? inner) : IHandlePayments
{
    public const decimal MaximumAmount = PaymentContractValidator.MaximumAmount;

    public Task<ServiceResult<Payment>> Handle(Payment payment, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var validationError = PaymentContractValidator.Validate(payment);
        if (validationError is not null)
        {
            return Task.FromResult(ServiceResult<Payment>.Invalid(validationError));
        }

        if (inner == null)
        {
            return Task.FromResult(ServiceResult<Payment>.Ok(payment));
        }
        
        return inner.Handle(payment, user, cancellationToken);
    }
}