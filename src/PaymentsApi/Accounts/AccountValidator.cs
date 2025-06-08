using System.Security.Claims;
using Models.Payments;
using Models.ResultPattern;
using PaymentsApi.Payments;

namespace PaymentsApi.Accounts;

public class AccountValidator(IHandlePayments? inner, IAccountsRepository accountsRepository) : IHandlePayments
{
    public async Task<ServiceResult<Payment>> Handle(Payment payment, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var fromAccount = await accountsRepository.GetAccount(
            payment.FromAccountId, 
            cancellationToken);

        if (fromAccount == null)
        {
            return ServiceResult<Payment>.Invalid("From account is invalid");
        }

        var toAccount = await accountsRepository.GetAccount(
            payment.ToAccountId, 
            cancellationToken);

        if (toAccount == null)
        {
            return ServiceResult<Payment>.Invalid("To account is invalid");
        }
        
        if (!fromAccount.UserId.Equals(user.Identity?.Name, StringComparison.InvariantCultureIgnoreCase))
        {
            // Flag as security issue
            Console.WriteLine("Attempted payment from an account that does not belong to the user");
            return ServiceResult<Payment>.Forbidden("You are not allowed to make payments from this account");
        }
        
        if (inner is null)
        {
            return ServiceResult<Payment>.Ok(payment);
        }
        return await inner.Handle(payment, user, cancellationToken);
    }
}