using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Starter.Infrastructure.Authentication;

namespace Starter.Api.Security;

internal static class AuthenticationSetup
{
    public const string RoleClaimType = "role";

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured through options so the signing key is read after user secrets and environment
        // variables are applied, and so tests can override it.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    RoleClaimType = RoleClaimType,
                };
            });

        services.AddAuthorizationBuilder().AddPolicies();
        services.AddHttpContextAccessor();
        services.AddScoped<Application.Common.Security.ICurrentUser, CurrentUser>();

        return services;
    }

    private static void AddPolicies(this Microsoft.AspNetCore.Authorization.AuthorizationBuilder builder) =>
        AuthorizationPolicies.AddPolicies(builder);
}
