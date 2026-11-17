using System.Text.Json.Serialization;
using Models.Payments;

namespace PaymentsApi.Payments;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreatePaymentRequest
{
    public string Currency { get; init; } = "EUR";
    public string Description { get; init; } = "";
    public decimal Amount { get; init; }
    public uint FromAccountId { get; init; }

    [KnownAccount]
    public uint ToAccountId { get; init; }

    public Payment ToPayment() => new()
    {
        Currency = Currency,
        Description = Description,
        Amount = Amount,
        FromAccountId = FromAccountId,
        ToAccountId = ToAccountId
    };
}
