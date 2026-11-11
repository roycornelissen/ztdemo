using Azure;
using Azure.Data.Tables;
using Models.Accounts;

namespace PaymentsApi.Accounts;

public class AccountsRepository([FromKeyedServices("accounts")] TableClient tableClient) : IAccountsRepository
{
    public async Task<Account?> GetAccount(
        uint accountId,
        CancellationToken cancellationToken = default)
    {
        try
        {
        var entity = await tableClient.GetEntityIfExistsAsync<AccountEntity>("accounts", accountId.ToString(),
            cancellationToken: cancellationToken);

        return entity.HasValue ?
            new Account
            {
                Id = accountId,
                Description = entity.Value!.Description,
                Owner = AccountOwner.TryParse(
                    entity.Value.OwnerTenantId,
                    entity.Value.OwnerObjectId,
                    out var owner)
                    ? owner
                    : null
            } : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}