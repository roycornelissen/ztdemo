using System.ComponentModel.DataAnnotations;
using Models.Accounts;

namespace AccountsApi.Accounts;

internal static class GetAccountEndpoint
{
    internal static async Task<IResult> Handle(
        HttpContext httpContext,
        AccountsRepository accountsRepository,
        [Range(1, uint.MaxValue)] uint id)
    {
        if (!AccountOwner.TryFromClaimsPrincipal(httpContext.User, out var owner))
        {
            return Results.Forbid();
        }

        var account = await accountsRepository.GetAccount(id, httpContext.RequestAborted);

        if (account == null)
        {
            return Results.NotFound();
        }

        if (account.Owner != owner)
        {
            return Results.Forbid();
        }
        return Results.Ok(account);
    }
}
