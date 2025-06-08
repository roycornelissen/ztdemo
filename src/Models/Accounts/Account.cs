namespace Models.Accounts;

public class Account
{
    public uint Id { get; init; }
    public string Description { get; init; } = "";
    public string UserId { get; init; } = "";
}