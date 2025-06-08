using Azure.Data.Tables;

namespace Processing.Transactions;

public class TransactionRepository([FromKeyedServices("transactions")] TableClient client)
{
    public async Task StoreTransactions(
        IEnumerable<BankTransaction> bankTransactions,
        CancellationToken cancellationToken = default)
    {
        foreach (var transaction in bankTransactions)
        {
            transaction.PartitionKey = transaction.AccountId.ToString();
            transaction.RowKey = Guid.NewGuid().ToString();
            await client.AddEntityAsync(transaction, cancellationToken);
        }
    }
}