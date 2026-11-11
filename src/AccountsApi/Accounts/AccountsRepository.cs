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

            if (!entity.HasValue)
            {
                return null;
            }

            var accountEntity = entity.Value!;
            return new Account
            {
                Id = accountId,
                Description = accountEntity.Description,
                Owner = AccountOwner.TryParse(
                    accountEntity.OwnerTenantId,
                    accountEntity.OwnerObjectId,
                    out var owner)
                    ? owner
                    : null
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<Account> GetAccounts(
        AccountOwner owner,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var entities = client
            .QueryAsync<AccountEntity>(
                a => a.PartitionKey == "accounts" &&
                    a.OwnerTenantId == owner.TenantId.ToString("D") &&
                    a.OwnerObjectId == owner.ObjectId.ToString("D"),
                cancellationToken: cancellationToken)
            .AsPages();
        
        await foreach (var page in entities)
        {
            foreach (var entity in page.Values)
            {
                yield return new Account
                {
                    Id = uint.Parse(entity.RowKey),
                    Description = entity.Description,
                    Owner = AccountOwner.TryParse(
                        entity.OwnerTenantId,
                        entity.OwnerObjectId,
                        out var entityOwner)
                        ? entityOwner
                        : null
                };
            }
        }
    }
}