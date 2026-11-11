namespace Processing.Payments;

public interface IAccountOwnershipReader
{
    Task<AccountOwnership?> GetAsync(uint accountId, CancellationToken cancellationToken = default);
}
