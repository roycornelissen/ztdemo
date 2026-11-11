namespace Models.Payments;

public static class PaymentContractValidator
{
    public const decimal MaximumAmount = 1_000_000m;

    public static string? Validate(Payment? payment)
    {
        if (payment is null)
        {
            return "Payment is required.";
        }
        if (string.IsNullOrWhiteSpace(payment.Currency))
        {
            return "The Currency field is required.";
        }
        if (payment.Currency.Length is < 3 or > 3)
        {
            return "The field Currency must be a string or array type with a minimum and maximum length of '3'.";
        }
        if (payment.Currency is not ("EUR" or "USD" or "GBP"))
        {
            return "Currency must be EUR, USD or GBP.";
        }
        if (string.IsNullOrWhiteSpace(payment.Description))
        {
            return "The Description field is required.";
        }
        if (payment.Description.Length > 30)
        {
            return "The field Description must be a string or array type with a maximum length of '30'.";
        }
        if (payment.Description.Any(char.IsControl))
        {
            return "Description must not contain control characters.";
        }
        // The ledger stores account IDs as signed 32-bit integers.
        if (payment.FromAccountId is 0 or > int.MaxValue || payment.ToAccountId is 0 or > int.MaxValue)
        {
            return "Account IDs must be between 1 and 2147483647.";
        }
        if (payment.FromAccountId == payment.ToAccountId)
        {
            return "From and To account IDs must be different.";
        }
        if (payment.Amount <= 0)
        {
            return "Deposit amount must be greater than zero.";
        }
        if (payment.Amount > MaximumAmount || decimal.Round(payment.Amount, 2) != payment.Amount)
        {
            return "Payment amount must not exceed 1000000 and must have at most two decimal places.";
        }

        return null;
    }
}
