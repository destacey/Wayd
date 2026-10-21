using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Wayd.Infrastructure.Auth.Local;

internal static class ConfigureServices
{
    internal const string LocalJwtScheme = AuthConstants.LocalJwtScheme;

    private const string DevOnlySecretPrefix = "DEV-ONLY-SECRET";

    internal static IServiceCollection AddLocalJwtAuth(this IServiceCollection services, IConfiguration config, IHostEnvironment environment)
    {
        var settings = config.GetSection(LocalJwtSettings.SectionName).Get<LocalJwtSettings>()
            ?? throw new InvalidOperationException($"Configuration section '{LocalJwtSettings.SectionName}' is missing.");

        if (string.IsNullOrWhiteSpace(settings.Secret))
        {
            throw new InvalidOperationException($"'{LocalJwtSettings.SectionName}:Secret' must be configured.");
        }

        if (!environment.IsDevelopment() && settings.Secret.StartsWith(DevOnlySecretPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"'{LocalJwtSettings.SectionName}:Secret' contains a development-only placeholder. "
                + "Configure a secure secret via user-secrets or environment variables for non-development environments.");
        }

        var signingKey = CreateSigningKey(settings.Secret);

        services.AddAuthentication()
            .AddJwtBearer(LocalJwtScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),

                    // The web app's tokens are typed JWT; an OAuth access token (at+jwt) is signed with the same key
                    // and must never pass here, even if issuers or audiences were ever configured to collide.
                    ValidTypes = ["JWT"],
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Support SignalR WebSocket token passing
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        // Skip this scheme early for tokens not intended for it.
                        // Peeking at the issuer before validation prevents IdentityModel
                        // from emitting IDX10205 diagnostic noise during cross-scheme attempts.
                        var token = context.Token;
                        if (token is null)
                        {
                            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                            if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                token = authHeader["Bearer ".Length..];
                            }
                        }

                        if (token is not null)
                        {
                            try
                            {
                                var jwt = new JsonWebToken(token);
                                if (!string.Equals(jwt.Issuer, settings.Issuer, StringComparison.OrdinalIgnoreCase))
                                {
                                    context.NoResult();
                                }
                            }
                            catch (ArgumentException)
                            {
                                // Malformed token — let normal validation surface the error
                            }
                        }

                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        // Safety net: if a cross-scheme token somehow reaches validation,
                        // suppress the issuer mismatch so the correct scheme can handle it.
                        if (context.Exception is SecurityTokenInvalidIssuerException)
                        {
                            context.NoResult();
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    internal static SymmetricSecurityKey CreateSigningKey(string secret)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        key.KeyId = Convert.ToBase64String(SHA256.HashData(key.Key))[..8];
        return key;
    }
}
