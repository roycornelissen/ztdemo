using Azure.Data.Tables;
using Models.Accounts;

namespace PaymentsApi.Accounts;

public class AccountsRepository([FromKeyedServices("accounts")] TableClient tableClient) : IAccountsRepository
{
    public async Task<Account?> GetAccount(
        uint accountId,
        CancellationToken cancellationToken = default)
    {
        var entity = await tableClient.GetEntityAsync<AccountEntity>("accounts", accountId.ToString(),
            cancellationToken: cancellationToken);

        return entity is not null ?
            new Account
            {
                Id = accountId,
                Description = entity.Value.Description,
                UserId = entity.Value.UserId
            } : null;
    }
}