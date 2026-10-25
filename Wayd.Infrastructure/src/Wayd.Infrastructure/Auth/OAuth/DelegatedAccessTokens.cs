using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NodaTime;
using Wayd.Infrastructure.Hosting;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.Auth.OAuth;

/// <summary>
/// The tokens a hosted MCP tool call carries through the API when its caller holds an OAuth access token. The access
/// token is bound to <c>/mcp</c>, so the call exchanges it in process for one bound to the API, naming the user as its
/// subject and the client as its actor (<c>act</c>, RFC 8693 §4.1), so the endpoints it reaches know an agent made the
/// call and which one.
/// </summary>
/// <remarks>
/// Only <see cref="InProcessDelegation"/> carries one to the scheme, and no HTTP request can set a feature, so a token
/// copied out of the process authenticates nothing. The web app's <c>LocalJwt</c> scheme refuses them on their type and
/// the <c>/mcp</c> scheme on their audience.
/// </remarks>
public static class DelegatedAccessTokens
{
    /// <summary>The claim naming the party acting for the subject (RFC 8693 §4.1).</summary>
    public const string ActorClaim = "act";

    /// <summary>
    /// The claim, on the principal the scheme builds, naming the OAuth client acting for the user. The token carries it
    /// inside <see cref="ActorClaim"/>.
    /// </summary>
    public const string ActingClientIdClaim = "acting_client_id";

    /// <summary>How long a token lives: it is minted for one request and only needs to last until that request authenticates.</summary>
    public static readonly Duration Lifetime = Duration.FromSeconds(60);

    /// <summary>
    /// Mints a token naming <paramref name="userId"/> as its subject and <paramref name="clientId"/> as the client acting
    /// for them, limited to <paramref name="scope"/>, typed <c>at+jwt</c>, issued by <paramref name="issuer"/> for the
    /// API's audience and signed with the web app's key.
    /// </summary>
    public static string Issue(IConfiguration config, string issuer, string userId, string clientId, TokenScope scope, Instant now)
    {
        var settings = OAuthAccessTokens.ReadSettings(config);

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            TokenType = OAuthAccessTokens.TokenType,
            Issuer = issuer,
            Audience = settings.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
            Claims = new Dictionary<string, object>
            {
                [ActorClaim] = new Dictionary<string, object> { [OAuthAccessTokens.ClientIdClaim] = clientId },
                [TokenScopes.ClaimType] = scope.ToScopeString(),
            },
            IssuedAt = now.ToDateTimeUtc(),
            NotBefore = now.ToDateTimeUtc(),
            Expires = now.Plus(Lifetime).ToDateTimeUtc(),
            SigningCredentials = new SigningCredentials(Local.ConfigureServices.CreateSigningKey(settings.Secret), SecurityAlgorithms.HmacSha256),
        });
    }

    /// <summary>
    /// Registers <see cref="AuthConstants.DelegatedAccessTokenScheme"/>, which accepts a token only from
    /// <see cref="InProcessDelegation"/>, and only when this API issued it for its own audience, it names an acting
    /// client, and its user is still active.
    /// </summary>
    internal static IServiceCollection AddDelegatedAccessTokenAuth(this IServiceCollection services)
    {
        services.AddAuthentication().AddJwtBearer(AuthConstants.DelegatedAccessTokenScheme, _ => { });

        services.AddOptions<JwtBearerOptions>(AuthConstants.DelegatedAccessTokenScheme)
            .Configure<IHttpContextAccessor, PublicUrl, IConfiguration>((options, httpContextAccessor, publicUrl, config) =>
            {
                var settings = OAuthAccessTokens.ReadSettings(config);

                options.MapInboundClaims = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidTypes = [OAuthAccessTokens.TokenType],
                    ValidateIssuer = true,
                    IssuerValidator = (issuer, _, _) => string.Equals(issuer, publicUrl.For(httpContextAccessor.HttpContext!.Request), StringComparison.Ordinal)
                        ? issuer
                        : throw new SecurityTokenInvalidIssuerException("The token was not issued by this API."),
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = Local.ConfigureServices.CreateSigningKey(settings.Secret),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ReadFromTheFeature,
                    OnTokenValidated = ResolveUser,
                };
            });

        return services;
    }

    /// <summary>
    /// Takes the token from <see cref="InProcessDelegation"/>, never a header, and refuses a request without one: only a
    /// request built inside the API can carry it.
    /// </summary>
    private static Task ReadFromTheFeature(MessageReceivedContext context)
    {
        if (context.HttpContext.Features.Get<InProcessDelegation>() is { } delegation)
            context.Token = delegation.Token;
        else
            context.Fail("A delegated token is accepted only on a request made inside the API.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Replaces the token's claims with the user's, as the personal access token scheme builds them, plus the acting
    /// client and the scope the user granted it. Refuses a token naming no client or no scope, and a user who has been
    /// deactivated since their client was authorized: the <c>/mcp</c> scheme checks only the session.
    /// </summary>
    private static async Task ResolveUser(TokenValidatedContext context)
    {
        if (context.SecurityToken is not JsonWebToken token || ActingClientId(token) is not { } clientId)
        {
            context.Fail("The token names no acting client.");
            return;
        }

        if (!token.TryGetPayloadValue<string>(TokenScopes.ClaimType, out var scope) || TokenScopes.Parse(scope) is null)
        {
            context.Fail("The token names no scope.");
            return;
        }

        var user = await context.HttpContext.RequestServices.GetRequiredService<WaydDbContext>().Users
            .AsNoTracking()
            .Where(u => u.Id == token.Subject && u.IsActive)
            .Select(u => new { u.Id, u.UserName, u.Email, u.EmployeeId })
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);
        if (user is null)
        {
            context.Fail("The token's user is not active.");
            return;
        }

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? "Unknown"),
            new(ActingClientIdClaim, clientId),
            new(TokenScopes.ClaimType, scope),
        ];
        if (!string.IsNullOrWhiteSpace(user.Email))
            claims.Add(new(ClaimTypes.Email, user.Email));
        if (user.EmployeeId is { } employeeId)
            claims.Add(new("EmployeeId", employeeId.ToString()));

        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthConstants.DelegatedAccessTokenScheme));
    }

    /// <summary>The <c>client_id</c> inside the token's <c>act</c> claim, or null when it has none.</summary>
    internal static string? ActingClientId(JsonWebToken token) =>
        token.TryGetPayloadValue<JsonElement>(ActorClaim, out var actor)
        && actor.ValueKind == JsonValueKind.Object
        && actor.TryGetProperty(OAuthAccessTokens.ClientIdClaim, out var clientId)
        && clientId.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(clientId.GetString())
            ? clientId.GetString()
            : null;
}

/// <summary>
/// The request feature carrying a delegated token to <see cref="AuthConstants.DelegatedAccessTokenScheme"/>. The hosted
/// MCP endpoint sets it on the request it builds for a tool call; a request that arrives over HTTP never has it.
/// </summary>
/// <param name="Token">The token minted for the request by <see cref="DelegatedAccessTokens.Issue"/>.</param>
public sealed record InProcessDelegation(string Token);
