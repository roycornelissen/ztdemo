using System.Globalization;
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
            var entity = new TableEntity(transaction.AccountId.ToString(CultureInfo.InvariantCulture), Guid.NewGuid().ToString())
            {
                ["AccountId"] = transaction.AccountId,
                ["Amount"] = transaction.Amount.ToString(CultureInfo.InvariantCulture),
                ["Description"] = transaction.Description
            };
            await client.AddEntityAsync(entity, cancellationToken);
        }
    }
}