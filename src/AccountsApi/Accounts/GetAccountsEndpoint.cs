using Models.Accounts;

namespace AccountsApi.Accounts;

internal static class GetAccountsEndpoint
{
    internal static async Task<IResult> Handle(HttpContext httpContext, AccountsRepository accountsRepository)
    {
        if (!AccountOwner.TryFromClaimsPrincipal(httpContext.User, out var owner))
        {
            return Results.Forbid();
        }

        var accounts = accountsRepository.GetAccounts(owner, httpContext.RequestAborted);
        var result = await accounts.ToArrayAsync(httpContext.RequestAborted);

        if (Array.Exists(result, account => account.Owner != owner))
        {
            return Results.Forbid();
        }
        return Results.Ok(result);
    }
}
