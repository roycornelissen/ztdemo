using System.Text.Json.Serialization;

namespace Models.Accounts;

public class Account
{
    public uint Id { get; init; }
    public string Description { get; init; } = "";

    [JsonIgnore]
    public AccountOwner? Owner { get; init; }
}