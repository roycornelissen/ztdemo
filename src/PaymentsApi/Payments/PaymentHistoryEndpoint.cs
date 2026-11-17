using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Models.Accounts;
using PaymentsApi.Accounts;

namespace PaymentsApi.Payments;

internal static class PaymentHistoryEndpoint
{
    internal static async Task<Results<Ok<IAsyncEnumerable<PaymentHistoryEntry>>, NotFound, ForbidHttpResult>> Handle(
        uint accountId,
        ClaimsPrincipal user,
        IAccountsRepository accounts,
        PaymentHistoryRepository history,
        CancellationToken cancellationToken)
    {
        if (!AccountOwner.TryFromClaimsPrincipal(user, out var owner))
        {
            return TypedResults.Forbid();
        }

        var account = await accounts.GetAccount(accountId, cancellationToken);
        if (account?.Owner != owner)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(history.GetEntries(accountId, cancellationToken));
    }
}
