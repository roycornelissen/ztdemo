using System.Security.Claims;
using System.Text.Json;
using Models.Accounts;
using Shouldly;

namespace PaymentsApiTests.Security;

public class AccountOwnerTests
{
    [Test]
    public void Requires_exactly_one_valid_tenant_and_object_claim()
    {
        var tenantId = PaymentsApiFactory.TenantId;
        var objectId = PaymentsApiFactory.ObjectId;
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tid", tenantId.ToString("D")),
            new Claim("oid", objectId.ToString("D"))
        ]));

        AccountOwner.TryFromClaimsPrincipal(principal, out var owner).ShouldBeTrue();
        owner.ShouldBe(new AccountOwner(tenantId, objectId));
    }

    [TestCase("tid")]
    [TestCase("oid")]
    public void Rejects_a_missing_required_claim(string missingClaim)
    {
        var claims = new List<Claim>
        {
            new("tid", PaymentsApiFactory.TenantId.ToString("D")),
            new("oid", PaymentsApiFactory.ObjectId.ToString("D"))
        };
        claims.RemoveAll(claim => claim.Type == missingClaim);

        AccountOwner.TryFromClaimsPrincipal(new ClaimsPrincipal(new ClaimsIdentity(claims)), out var owner)
            .ShouldBeFalse();
        owner.ShouldBeNull();
    }

    [Test]
    public void Rejects_ambiguous_duplicate_claims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tid", PaymentsApiFactory.TenantId.ToString("D")),
            new Claim("tid", PaymentsApiFactory.TenantId.ToString("D")),
            new Claim("oid", PaymentsApiFactory.ObjectId.ToString("D"))
        ]));

        AccountOwner.TryFromClaimsPrincipal(principal, out var owner).ShouldBeFalse();
        owner.ShouldBeNull();
    }

    [Test]
    public void Rejects_empty_guid_claims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tid", Guid.Empty.ToString("D")),
            new Claim("oid", PaymentsApiFactory.ObjectId.ToString("D"))
        ]));

        AccountOwner.TryFromClaimsPrincipal(principal, out var owner).ShouldBeFalse();
        owner.ShouldBeNull();
    }

    [Test]
    public void Account_owner_is_not_exposed_in_account_json()
    {
        var account = new Account
        {
            Id = 1,
            Description = "Everyday account",
            Owner = new AccountOwner(PaymentsApiFactory.TenantId, PaymentsApiFactory.ObjectId)
        };

        var json = JsonSerializer.Serialize(account);

        json.ShouldContain("\"Id\":1");
        json.ShouldNotContain("TenantId");
        json.ShouldNotContain("ObjectId");
    }
}
