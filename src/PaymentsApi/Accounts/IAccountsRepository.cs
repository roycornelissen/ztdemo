using Models.Accounts;

namespace PaymentsApi.Accounts;

public interface IAccountsRepository
{
    Task<Account?> GetAccount(
        uint accountId,
        CancellationToken cancellationToken = default);
}