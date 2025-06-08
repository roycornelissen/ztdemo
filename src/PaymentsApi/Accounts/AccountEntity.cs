using Azure;
using Azure.Data.Tables;

namespace PaymentsApi.Accounts;

public class AccountEntity : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    public string UserId { get; set; }
    public string Description { get; set; } = "";
}