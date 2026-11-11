using Azure.Data.Tables;
using Models.Accounts;

namespace Processing.Payments;

public sealed class TableAccountOwnershipReader(
    [FromKeyedServices("accounts")] TableClient tableClient) : IAccountOwnershipReader
{
    public async Task<AccountOwnership?> GetAsync(
        uint accountId,
        CancellationToken cancellationToken = default)
    {
        var response = await tableClient.GetEntityIfExistsAsync<TableEntity>(
            "accounts",
            accountId.ToString(),
            cancellationToken: cancellationToken);

        if (!response.HasValue)
        {
            return null;
        }

        var entity = response.Value!;
        var tenantId = entity.TryGetValue("OwnerTenantId", out var tenantValue)
            ? tenantValue as string
            : null;
        var objectId = entity.TryGetValue("OwnerObjectId", out var objectValue)
            ? objectValue as string
            : null;
        return new AccountOwnership(
            AccountOwner.TryParse(tenantId, objectId, out var owner) ? owner : null);
    }
}
