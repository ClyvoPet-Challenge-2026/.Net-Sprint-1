using System.Globalization;
using System.Security.Cryptography;
using ClyvoCare.Application.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ClyvoCare.API.Extensions;

public static class ClyvoCareAuthenticationExtensions
{
    public static IServiceCollection AddClyvoCareAuthentication(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<RSA>(_ =>
        {
            var path = configuration["Jwt:PublicKeyPath"] ?? ".keys/public_key.pem";
            var pem = File.ReadAllText(path);
            if (pem.Contains("PRIVATE KEY", StringComparison.Ordinal))
                throw new InvalidOperationException("Configure only the RSA public key for this API.");
            var rsa = RSA.Create();
            try
            {
                rsa.ImportFromPem(pem);
                if (rsa.KeySize < 2048)
                    throw new InvalidOperationException("The RSA public key must be at least 2048 bits.");
                return rsa;
            }
            catch
            {
                rsa.Dispose();
                throw;
            }
        });
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<RSA>((options, rsa) =>
            {
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new RsaSecurityKey(rsa),
                    RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "clyvocare-api",
                    // Java does not issue an audience claim in the shared token contract.
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var user = context.Principal!;
                        var email = user.FindFirst("sub")?.Value;
                        var role = user.FindFirst("role")?.Value;
                        if (user.FindAll("ownerId").Count() != 1 || user.FindAll("sub").Count() != 1
                            || user.FindAll("role").Count() != 1
                            || !long.TryParse(user.FindFirst("ownerId")?.Value, NumberStyles.None,
                                CultureInfo.InvariantCulture, out var ownerId) || ownerId <= 0
                            || string.IsNullOrWhiteSpace(email) || role is not ("ADMIN" or "OWNER"))
                        {
                            context.Fail("Invalid account identity.");
                            return;
                        }
                        var identities = context.HttpContext.RequestServices.GetRequiredService<IClinicalIdentityRepository>();
                        if (!await identities.IsActiveOwnerAsync(ownerId, email, role, context.HttpContext.RequestAborted))
                            context.Fail("Token does not match an active account.");
                    }
                };
            }).ValidateOnStart();
        services.AddAuthorization();
        return services;
    }
}
