using System.ComponentModel.DataAnnotations;

namespace Models.Payments;

public record Payment
{
    [Required] [MinLength(3)] [MaxLength(3)]
    public string Currency { get; init; } = "EUR";
    [Required] [MinLength(1)] [MaxLength(30)] public string Description { get; init; } = "";
    public decimal Amount { get; init; }
    public uint FromAccountId { get; init; }
    public uint ToAccountId { get; init; }
}
