using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddDelegatedScopeAuthorization(
        this IServiceCollection services, string policyName, string requiredScope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredScope);

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireAssertion(context => context.User.Claims
                .Where(claim => claim.Type is "scp" or "scope")
                .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Contains(requiredScope, StringComparer.Ordinal))
            .Build();

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(policy)
            .SetFallbackPolicy(policy)
            .AddPolicy(policyName, policy);

        return services;
    }
}
