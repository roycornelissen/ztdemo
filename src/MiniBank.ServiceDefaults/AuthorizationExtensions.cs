using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;

namespace Microsoft.Extensions.Hosting;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddDelegatedScopeAuthorization(
        this IServiceCollection services, string requiredScope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredScope);

        services.AddRequiredScopeAuthorization();

        var scopePolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireScope(requiredScope)
            .Build();

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(scopePolicy)
            .SetFallbackPolicy(scopePolicy);

        return services;
    }
}
