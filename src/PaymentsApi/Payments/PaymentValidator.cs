using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Models.Payments;
using Models.ResultPattern;

namespace PaymentsApi.Payments;

public class PaymentValidator(IHandlePayments? inner) : IHandlePayments
{
    public Task<ServiceResult<Payment>> Handle(Payment payment, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var context = new ValidationContext(payment);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(payment, context, results, validateAllProperties: true);

        if (!isValid)
        {
            var message = string.Join(", ", results.Select(r => r.ErrorMessage).ToArray());
            return Task.FromResult(ServiceResult<Payment>.Invalid(message));
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