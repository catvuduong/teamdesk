using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Api.Auth;

public static class AuthSetup
{
    public const string Issuer = "teamdesk";
    public const string Audience = "teamdesk-web";
    public const string StaffPolicy = "Staff";
    public const string AdminPolicy = "Admin";

    public static IServiceCollection AddTeamDeskAuth(this IServiceCollection services)
    {
        var key = SigningKey.Derive(
            Environment.GetEnvironmentVariable("JWT_SECRET"),
            Environment.GetEnvironmentVariable("DATABASE_URL"));
        services.AddSingleton(new TokenService(key));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep the short claim names ("sub", "role", "name") as they are in the token
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = Issuer,
                    ValidateAudience = true,
                    ValidAudience = Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role",
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(StaffPolicy, policy => policy.RequireRole(Roles.Agent, Roles.Admin));
            options.AddPolicy(AdminPolicy, policy => policy.RequireRole(Roles.Admin));
        });
        return services;
    }
}
