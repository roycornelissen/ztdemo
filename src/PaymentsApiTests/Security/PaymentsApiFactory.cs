using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace PaymentsApiTests.Security;

public sealed class PaymentsApiFactory(
    Action<IServiceCollection>? configureServices = null,
    string environment = "Development",
    bool exposeHealthEndpoints = false)
    : WebApplicationFactory<PaymentsApi.Payments.PaymentHandler>
{
    private const string Issuer = "https://issuer.example.test";
    private const string Audience = "minibank-test";
    public static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid ObjectId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly SymmetricSecurityKey Key =
        new(Encoding.UTF8.GetBytes("test-only-signing-key-not-a-production-credential-1234567890"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        if (exposeHealthEndpoints)
        {
            builder.UseSetting("HealthChecks:ExposeEndpoints", "true");
        }

        // Satisfy non-development startup validation without requesting an Azure token.
        builder.UseSetting("AZURE_CLIENT_ID", "00000000-0000-0000-0000-000000000003");
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                ConfigureOfflineValidation(options));
            configureServices?.Invoke(services);
        });
    }

    internal static void ConfigureOfflineValidation(JwtBearerOptions options)
    {
        // Offline issuer configuration; production JWT validation is not replaced by a fake scheme.
        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
            new OpenIdConnectConfiguration { Issuer = Issuer });
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = Key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    }

    public static string CreateToken(
        string? scope = "Payment.Read", string? audience = null, bool expired = false,
        string claimType = "scp", bool wrongSignature = false, bool includeOwner = false)
    {
        var claims = new List<Claim> { new("sub", "demo-user") };
        if (scope is not null)
        {
            claims.Add(new Claim(claimType, scope));
        }
        if (includeOwner)
        {
            claims.Add(new Claim("tid", TenantId.ToString()));
            claims.Add(new Claim("oid", ObjectId.ToString()));
        }

        var key = wrongSignature
            ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes("another-test-only-signing-key-that-is-not-trusted-1234567890"))
            : Key;
        var token = new JwtSecurityToken(
            Issuer, audience ?? Audience, claims,
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: DateTime.UtcNow.AddMinutes(expired ? -1 : 5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
