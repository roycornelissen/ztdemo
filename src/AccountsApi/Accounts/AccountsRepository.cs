using System.Runtime.CompilerServices;
using Azure.Data.Tables;
using Models.Accounts;

namespace AccountsApi.Accounts;

public class AccountsRepository([FromKeyedServices("accounts")] TableClient client)
{
    public async IAsyncEnumerable<Account> GetAccounts(
        string userId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var entities = client
            .QueryAsync<AccountEntity>(a => a.PartitionKey == "accounts" && userId == a.UserId, cancellationToken: cancellationToken)
            .AsPages();
        
        await foreach (var page in entities)
        {
            foreach (var entity in page.Values)
            {
                yield return new Account
                {
                    Id = uint.Parse(entity.RowKey),
                    Description = entity.Description,
                    UserId = entity.UserId
                };
            }
        }
    }
}