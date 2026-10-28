using System.Globalization;
using System.Security.Cryptography;
using ClyvoCare.API.Auth;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ClyvoCare.API.Extensions;

public static class ClyvoCareAuthenticationExtensions
{
    /// <summary>Valida tokens RS256 da Java usando somente a chave pública externa.</summary>
    public static IServiceCollection AddClyvoCareAuthentication(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<RSA>(_ =>
        {
            var keyPath = configuration["Jwt:PublicKeyPath"] ?? ".keys/public_key.pem";
            var rsa = RSA.Create();
            try
            {
                rsa.ImportFromPem(File.ReadAllText(keyPath));
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
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new RsaSecurityKey(rsa),
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "clyvocare-api",
                    // Os tokens emitidos pela Java não possuem audience.
                    ValidateAudience = false,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var user = context.Principal!;
                        var email = user.FindFirst("sub")?.Value;
                        var role = user.FindFirst("role")?.Value;
                        var identities = context.HttpContext.RequestServices
                            .GetRequiredService<IClinicalIdentityRepository>();
                        if (!long.TryParse(user.FindFirst("ownerId")?.Value, NumberStyles.None,
                                CultureInfo.InvariantCulture, out var ownerId) || ownerId <= 0
                            || string.IsNullOrWhiteSpace(email) || role is not ("ADMIN" or "OWNER")
                            || !identities.IsActiveOwner(ownerId, email, role))
                            context.Fail("A identidade do token não corresponde a uma conta ativa.");
                        return Task.CompletedTask;
                    }
                };
            });
        services.AddAuthorization();
        return services;
    }
}
