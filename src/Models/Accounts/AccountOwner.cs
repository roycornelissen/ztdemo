using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace Models.Accounts;

public sealed record AccountOwner(Guid TenantId, Guid ObjectId)
{
    public static bool TryFromClaimsPrincipal(
        ClaimsPrincipal principal,
        [NotNullWhen(true)] out AccountOwner? owner)
    {
        owner = null;
        if (!TryGetGuidClaim(principal, "tid", out var tenantId) ||
            !TryGetGuidClaim(principal, "oid", out var objectId))
        {
            return false;
        }

        owner = new AccountOwner(tenantId, objectId);
        return true;
    }

    public static bool TryParse(
        string? tenantId,
        string? objectId,
        [NotNullWhen(true)] out AccountOwner? owner)
    {
        owner = null;
        if (!Guid.TryParse(tenantId, out var parsedTenantId) ||
            parsedTenantId == Guid.Empty ||
            !Guid.TryParse(objectId, out var parsedObjectId) ||
            parsedObjectId == Guid.Empty)
        {
            return false;
        }

        owner = new AccountOwner(parsedTenantId, parsedObjectId);
        return true;
    }

    private static bool TryGetGuidClaim(ClaimsPrincipal principal, string claimType, out Guid value)
    {
        value = default;
        var claims = principal.FindAll(claimType).ToArray();
        return claims.Length == 1 &&
            Guid.TryParse(claims[0].Value, out value) &&
            value != Guid.Empty;
    }
}
