namespace Models.Payments;

public record Payment
{
    public string Currency { get; init; } = "EUR";
    public string Description { get; init; } = "";
    public decimal Amount { get; init; }
    public uint FromAccountId { get; init; }
    public uint ToAccountId { get; init; }
}
