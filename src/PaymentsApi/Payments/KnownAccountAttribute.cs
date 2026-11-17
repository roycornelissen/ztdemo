using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Models.Payments;
using PaymentsApi.Accounts;

namespace PaymentsApi.Payments;

[AttributeUsage(AttributeTargets.Property)]
public sealed class KnownAccountAttribute : AsyncValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context) =>
        throw new InvalidOperationException("Validate this attribute with IsValidAsync.");

    protected override async Task<ValidationResult?> IsValidAsync(
        object? value,
        ValidationContext context,
        CancellationToken cancellationToken)
    {
        if (value is not uint accountId || accountId is 0 or > int.MaxValue)
        {
            return ValidationResult.Success;
        }

        if (context.ObjectInstance is not CreatePaymentRequest request)
        {
            throw new InvalidOperationException(
                $"{nameof(KnownAccountAttribute)} can only validate {nameof(CreatePaymentRequest)}.");
        }

        if (PaymentContractValidator.Validate(request.ToPayment()) is not null)
        {
            return ValidationResult.Success;
        }

        var accounts = context.GetRequiredService<IAccountsRepository>();
        var account = await accounts.GetAccount(accountId, cancellationToken);

        return account is not null
            ? ValidationResult.Success
            : new ValidationResult(
                "To account is invalid",
                [context.MemberName ?? nameof(CreatePaymentRequest.ToAccountId)]);
    }
}
