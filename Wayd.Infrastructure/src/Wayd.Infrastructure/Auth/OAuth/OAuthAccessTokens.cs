using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Wayd.Infrastructure.Auth.Local;
using Wayd.Infrastructure.Hosting;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.Auth.OAuth;

/// <summary>
/// The access tokens Wayd's authorization server issues: JWTs typed <c>at+jwt</c> (RFC 9068), whose issuer is the
/// API's <see cref="PublicUrl"/> and whose audience is the protected resource. They are signed with the same key as
/// the web app's tokens, but differ in issuer, audience and type, so neither can pass as the other.
/// </summary>
public static class OAuthAccessTokens
{
    /// <summary>The JWT <c>typ</c> header of an OAuth access token (RFC 9068 §2.1).</summary>
    public const string TokenType = "at+jwt";

    /// <summary>The claim naming the client a token was issued to (RFC 9068 §2.2).</summary>
    public const string ClientIdClaim = "client_id";

    /// <summary>The claim naming the refresh-token session a token was issued from, so revoking the session ends it.</summary>
    public const string SessionIdClaim = "sid";

    /// <summary>
    /// Whether <paramref name="authorizationHeader"/> carries a bearer token typed as an OAuth access token. It reads
    /// the header without validating anything, so use it only to choose which scheme validates the token.
    /// </summary>
    public static bool IsAccessToken(string? authorizationHeader)
    {
        if (authorizationHeader is null || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            return string.Equals(new JsonWebToken(authorizationHeader["Bearer ".Length..].Trim()).Typ, TokenType, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Registers <see cref="AuthConstants.OAuthAccessTokenScheme"/>, which accepts only these tokens, and only when
    /// their issuer and audience are this API's for the current request and the session they were issued from is still
    /// live. No policy uses it unless the host names it.
    /// </summary>
    internal static IServiceCollection AddOAuthAccessTokenAuth(this IServiceCollection services)
    {
        services.AddAuthentication().AddJwtBearer(AuthConstants.OAuthAccessTokenScheme, _ => { });

        services.AddOptions<JwtBearerOptions>(AuthConstants.OAuthAccessTokenScheme)
            .Configure<IHttpContextAccessor, PublicUrl, IOptions<AuthorizationServerOptions>, IConfiguration>(
                (options, httpContextAccessor, publicUrl, serverOptions, config) =>
                {
                    string Issuer() => publicUrl.For(httpContextAccessor.HttpContext!.Request);

                    options.MapInboundClaims = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidTypes = [TokenType],
                        ValidateIssuer = true,
                        IssuerValidator = (issuer, _, _) => string.Equals(issuer, Issuer(), StringComparison.Ordinal)
                            ? issuer
                            : throw new SecurityTokenInvalidIssuerException("The token was not issued by this API's authorization server."),
                        ValidateAudience = true,
                        AudienceValidator = (audiences, _, _) =>
                            audiences.Contains(Issuer() + serverOptions.Value.ResourcePath, StringComparer.Ordinal),
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = Local.ConfigureServices.CreateSigningKey(ReadSettings(config).Secret),
                        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1),
                    };
                    options.Events = new JwtBearerEvents { OnTokenValidated = RequireLiveSession };
                });

        return services;
    }

    /// <summary>
    /// Refuses a token whose session has been revoked or has expired, so revoking a connector in the sessions list, or a
    /// replayed authorization code revoking it, ends its access at once rather than when the token expires. One lookup
    /// by primary key per request.
    /// </summary>
    private static async Task RequireLiveSession(TokenValidatedContext context)
    {
        if (context.SecurityToken is not JsonWebToken token
            || !token.TryGetPayloadValue<string>(SessionIdClaim, out var sid)
            || !Guid.TryParseExact(sid, "N", out var sessionId))
        {
            context.Fail("The token names no session.");
            return;
        }

        var services = context.HttpContext.RequestServices;
        var now = services.GetRequiredService<IDateTimeProvider>().Now;
        var live = await services.GetRequiredService<WaydDbContext>().UserRefreshTokens
            .AsNoTracking()
            .AnyAsync(t => t.Id == sessionId && t.UserId == token.Subject && t.RevokedAt == null && t.ExpiresAt > now, context.HttpContext.RequestAborted);
        if (!live)
            context.Fail("The token's session has ended.");
    }

    /// <summary>The web app's token settings, whose secret signs these tokens too.</summary>
    internal static LocalJwtSettings ReadSettings(IConfiguration config) =>
        config.GetSection(LocalJwtSettings.SectionName).Get<LocalJwtSettings>()
        ?? throw new InvalidOperationException($"Configuration section '{LocalJwtSettings.SectionName}' is missing.");
}
