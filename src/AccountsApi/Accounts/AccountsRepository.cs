using Azure;
using Azure.Data.Tables;
using Models.Accounts;
using System.Runtime.CompilerServices;

namespace AccountsApi.Accounts;

public class AccountsRepository([FromKeyedServices("accounts")] TableClient client)
{
    public async Task<Account?> GetAccount(
        uint accountId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await client.GetEntityIfExistsAsync<AccountEntity>("accounts", accountId.ToString(),
                cancellationToken: cancellationToken);

            return entity.HasValue ?
                new Account
                {
                    Id = accountId,
                    Description = entity.Value.Description,
                    UserId = entity.Value.UserId
                } : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

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