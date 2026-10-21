using System.Security.Claims;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using NodaTime;
using Wayd.Infrastructure.Auth.Local;
using Wayd.Infrastructure.Hosting;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.Auth.OAuth;

/// <inheritdoc />
internal sealed class AuthorizationServer(
    WaydDbContext db,
    IOAuthClientResolver clientResolver,
    IRefreshTokenStore refreshTokenStore,
    ISessionContextAccessor sessionContext,
    UserManager<ApplicationUser> userManager,
    IUserIdentityStore userIdentityStore,
    IDateTimeProvider dateTimeProvider,
    PublicUrl publicUrl,
    WebClientUrl webClientUrl,
    IOptions<AuthorizationServerOptions> options,
    IConfiguration config,
    ILogger<AuthorizationServer> logger) : IAuthorizationServer
{
    /// <summary>How long a row outlives its request, so a code presented again is still recognised as a replay.</summary>
    private static readonly Duration _retention = Duration.FromDays(1);

    /// <summary>The longest <c>state</c> kept; a longer one is refused rather than truncated, since it is returned verbatim.</summary>
    private const int MaxStateLength = 1024;

    /// <summary>The longest <c>client_id</c> or <c>redirect_uri</c> stored, matching their columns.</summary>
    private const int MaxUriLength = 2048;

    /// <summary>The answer for a handle that is unknown, expired or already decided, which do not need telling apart.</summary>
    private const string NotPending = "This authorization request has expired or has already been answered.";

    /// <summary>The answer for any code that cannot be redeemed, so a guess learns nothing about why.</summary>
    private const string InvalidCode = "The authorization code is invalid, expired, or already used.";

    private readonly WaydDbContext _db = db;
    private readonly IOAuthClientResolver _clientResolver = clientResolver;
    private readonly IRefreshTokenStore _refreshTokenStore = refreshTokenStore;
    private readonly ISessionContextAccessor _sessionContext = sessionContext;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly IUserIdentityStore _userIdentityStore = userIdentityStore;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly PublicUrl _publicUrl = publicUrl;
    private readonly WebClientUrl _webClientUrl = webClientUrl;
    private readonly AuthorizationServerOptions _options = options.Value;
    private readonly IConfiguration _config = config;
    private readonly ILogger<AuthorizationServer> _logger = logger;

    /// <inheritdoc />
    public async Task<AuthorizeOutcome> Authorize(HttpRequest request, CancellationToken cancellationToken)
    {
        var query = request.Query;

        // Every error is shown to the user rather than sent to the redirect URI. With Client ID Metadata Documents
        // anyone can be a client and name any redirect URI, so redirecting an error before the user has seen who is
        // asking would let a malformed request bounce a browser from Wayd's domain to any site (RFC 9700 §4.11.2).
        // The client hears back only once the user approves or denies.
        static AuthorizeOutcome Refuse(string description) => new AuthorizeOutcome.ShowError(description);

        if (!TrySingle(query, "client_id", out var clientId))
            return Refuse("The request must name its client once, in client_id.");
        if (!TrySingle(query, "redirect_uri", out var redirectUri))
            return Refuse("The request must name its redirect_uri once.");
        if (clientId.Length > MaxUriLength || redirectUri.Length > MaxUriLength)
            return Refuse($"client_id and redirect_uri must each be at most {MaxUriLength} characters.");

        foreach (var name in new[] { "response_type", "state", "code_challenge", "code_challenge_method", "resource" })
        {
            if (query[name].Count > 1)
                return Refuse($"{name} must not be repeated.");
        }

        var responseType = query["response_type"].ToString();
        if (responseType != "code")
            return Refuse("Only the authorization code flow (response_type=code) is supported.");

        var state = query["state"].ToString();
        if (state.Length == 0 || state.Length > MaxStateLength)
            return Refuse($"state is required, and must be at most {MaxStateLength} characters.");

        var codeChallenge = query["code_challenge"].ToString();
        if (codeChallenge.Length == 0)
            return Refuse("PKCE is required: send code_challenge and code_challenge_method=S256.");
        if (query["code_challenge_method"].ToString() != Pkce.S256)
            return Refuse("code_challenge_method must be S256.");
        if (!Pkce.IsChallenge(codeChallenge))
            return Refuse("code_challenge must be the base64url-encoded SHA-256 of the code verifier.");

        var issuer = _publicUrl.For(request);
        var resource = issuer + _options.ResourcePath;
        var requestedResource = query["resource"].ToString();
        if (requestedResource.Length > 0 && requestedResource != resource)
            return Refuse($"The only resource this server issues tokens for is {resource}.");

        if (_webClientUrl.Value is null)
        {
            _logger.LogError("An OAuth authorization request was refused because {Setting} is not set, so there is no consent page to send the user to.",
                WebClientUrl.ConfigurationKey);
            return new AuthorizeOutcome.ShowError("Wayd is not set up to connect applications yet. Ask your administrator.", StatusCodes.Status500InternalServerError);
        }

        // The client's document is fetched last, so a request that fails a cheaper check never makes Wayd fetch a URL
        // the requester chose.
        var client = await _clientResolver.Resolve(clientId, cancellationToken);
        if (client.IsFailure)
            return Refuse(client.Error);

        if (!client.Value.RedirectUris.Contains(redirectUri, StringComparer.Ordinal))
            return Refuse("The redirect_uri is not one of those the client's metadata document lists.");

        var now = _dateTimeProvider.Now;
        await PurgeExpired(now, cancellationToken);

        var handle = OAuthSecrets.Create();
        var clientHost = new Uri(client.Value.ClientId).IdnHost;
        _db.OAuthAuthorizations.Add(new OAuthAuthorization
        {
            Id = Guid.NewGuid(),
            RequestHandleHash = OAuthSecrets.Hash(handle),
            ClientId = client.Value.ClientId,
            ClientName = ClientDisplayName.Clean(client.Value.Name, clientHost),
            RedirectUri = redirectUri,
            CodeChallenge = codeChallenge,
            Resource = resource,
            State = state,
            Issuer = issuer,
            Status = OAuthAuthorizationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.Plus(Duration.FromTimeSpan(_options.RequestLifetime)),
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new AuthorizeOutcome.Redirect(QueryHelpers.AddQueryString(_webClientUrl.Value + _options.ConsentPath, "request", handle));
    }

    /// <inheritdoc />
    public async Task<Result<PendingAuthorization>> FindPending(string handle, CancellationToken cancellationToken)
    {
        var authorization = await FindPendingRow(handle, tracked: false, cancellationToken);
        if (authorization is null)
            return Result.Failure<PendingAuthorization>(NotPending);

        var clientHost = new Uri(authorization.ClientId).IdnHost;
        var redirect = new Uri(authorization.RedirectUri);
        var redirectHost = redirect.Scheme is "https" or "http" ? redirect.IdnHost : null;
        var loopback = redirectHost is not null && redirect.IsLoopback;

        // A loopback or app-scheme redirect stays on the user's own machine, so a host other than the client's is
        // expected there and is not the warning sign it is for a web redirect.
        return new PendingAuthorization(
            authorization.ClientId,
            clientHost,
            authorization.ClientName,
            authorization.RedirectUri,
            redirectHost,
            redirectHost is not null && !loopback && !string.Equals(redirectHost, clientHost, StringComparison.OrdinalIgnoreCase),
            loopback,
            authorization.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task<Result<string>> Approve(string handle, string userId, CancellationToken cancellationToken)
    {
        var authorization = await FindPendingRow(handle, tracked: true, cancellationToken);
        if (authorization is null)
            return Result.Failure<string>(NotPending);

        var now = _dateTimeProvider.Now;
        var code = OAuthSecrets.Create();
        authorization.Status = OAuthAuthorizationStatus.Approved;
        authorization.UserId = userId;
        authorization.DecidedAt = now;
        authorization.CodeHash = OAuthSecrets.Hash(code);
        authorization.CodeExpiresAt = now.Plus(Duration.FromTimeSpan(_options.CodeLifetime));

        if (!await TrySave(cancellationToken))
            return Result.Failure<string>(NotPending);

        return QueryHelpers.AddQueryString(authorization.RedirectUri, new Dictionary<string, string?>
        {
            ["code"] = code,
            ["state"] = authorization.State,
            ["iss"] = authorization.Issuer,
        });
    }

    /// <inheritdoc />
    public async Task<Result<string>> Deny(string handle, string userId, CancellationToken cancellationToken)
    {
        var authorization = await FindPendingRow(handle, tracked: true, cancellationToken);
        if (authorization is null)
            return Result.Failure<string>(NotPending);

        authorization.Status = OAuthAuthorizationStatus.Denied;
        authorization.UserId = userId;
        authorization.DecidedAt = _dateTimeProvider.Now;

        if (!await TrySave(cancellationToken))
            return Result.Failure<string>(NotPending);

        return ErrorRedirect(authorization.RedirectUri, "access_denied", "The user declined to authorize the client.", authorization.State, authorization.Issuer);
    }

    /// <inheritdoc />
    public async Task<TokenOutcome> Token(HttpRequest request, CancellationToken cancellationToken)
    {
        const string FormRequired = "The request body must be application/x-www-form-urlencoded.";

        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType)
            || !mediaType.MediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return Refused("invalid_request", FormRequired);
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException)
        {
            return Refused("invalid_request", FormRequired);
        }

        if (form.Any(field => field.Value.Count > 1))
            return Refused("invalid_request", "No parameter may be repeated.");

        var clientId = form["client_id"].ToString();
        if (clientId.Length == 0)
            return Refused("invalid_request", "client_id is required.");

        var issuer = _publicUrl.For(request);
        var resource = issuer + _options.ResourcePath;
        var requestedResource = form["resource"].ToString();
        if (requestedResource.Length > 0 && requestedResource != resource)
            return Refused("invalid_target", $"The only resource this server issues tokens for is {resource}.");

        return form["grant_type"].ToString() switch
        {
            "" => Refused("invalid_request", "grant_type is required."),
            "authorization_code" => await RedeemCode(form, clientId, issuer, resource, cancellationToken),
            "refresh_token" => await Refresh(form, clientId, issuer, resource, cancellationToken),
            _ => Refused("unsupported_grant_type", "Only the authorization_code and refresh_token grants are supported."),
        };
    }

    /// <summary>
    /// Redeems an authorization code. The code is spent whether or not the rest of the request holds, so a wrong
    /// verifier cannot be retried against it. A successful redemption marks the code spent, records the session it
    /// opens, and creates that session in one save, so anyone who later sees the code spent also sees the session to
    /// revoke; a redemption that loses a race to another is treated as the replay it is.
    /// </summary>
    private async Task<TokenOutcome> RedeemCode(IFormCollection form, string clientId, string issuer, string resource, CancellationToken cancellationToken)
    {
        var code = form["code"].ToString();
        var redirectUri = form["redirect_uri"].ToString();
        var verifier = form["code_verifier"].ToString();
        if (code.Length == 0 || redirectUri.Length == 0 || verifier.Length == 0)
            return Refused("invalid_request", "code, redirect_uri and code_verifier are required.");

        var codeHash = OAuthSecrets.Hash(code);
        var authorization = await _db.OAuthAuthorizations.FirstOrDefaultAsync(a => a.CodeHash == codeHash, cancellationToken);
        if (authorization is null)
            return Refused("invalid_grant", InvalidCode);

        if (authorization.Status == OAuthAuthorizationStatus.Redeemed)
        {
            await RevokeReplayedSession(authorization, cancellationToken);
            return Refused("invalid_grant", InvalidCode);
        }

        var now = _dateTimeProvider.Now;
        if (authorization.Status != OAuthAuthorizationStatus.Approved || authorization.CodeExpiresAt <= now || authorization.UserId is null)
            return Refused("invalid_grant", InvalidCode);

        var (refusal, user) = await CheckRedemption(authorization, clientId, redirectUri, verifier, issuer, resource, cancellationToken);

        authorization.Status = OAuthAuthorizationStatus.Redeemed;
        authorization.RedeemedAt = now;

        if (refusal is not null)
        {
            if (!await TrySave(cancellationToken))
                await RevokeAfterLostRace(authorization.Id, cancellationToken);
            return refusal;
        }

        var sessionId = Guid.NewGuid();
        authorization.SessionId = sessionId;

        ClientSession session;
        try
        {
            // The store's save commits the authorization's changes with the new session.
            session = await _refreshTokenStore.IssueForClient(sessionId, user!.Id, clientId, authorization.ClientName, _sessionContext.Current, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RevokeAfterLostRace(authorization.Id, cancellationToken);
            return Refused("invalid_grant", InvalidCode);
        }

        return IssueAccessToken(user.Id, clientId, sessionId, issuer, resource, session.Token);
    }

    /// <summary>
    /// Checks a redemption against the request the code was issued for, and that the user who approved it may still
    /// sign in. Answers the refusal, or the user when everything holds.
    /// </summary>
    private async Task<(TokenOutcome.Refused? Refusal, ApplicationUser? User)> CheckRedemption(
        OAuthAuthorization authorization,
        string clientId,
        string redirectUri,
        string verifier,
        string issuer,
        string resource,
        CancellationToken cancellationToken)
    {
        if (authorization.ClientId != clientId || authorization.RedirectUri != redirectUri)
            return (Refused("invalid_grant", "client_id and redirect_uri must match the authorization request."), null);
        if (!Pkce.Verify(verifier, authorization.CodeChallenge))
            return (Refused("invalid_grant", "The code_verifier does not match the code_challenge."), null);
        if (authorization.Resource != resource || authorization.Issuer != issuer)
            return (Refused("invalid_grant", "The authorization code was issued for a different resource."), null);

        var user = await FindUsableUser(authorization.UserId!, cancellationToken);
        return user is null
            ? (Refused("invalid_grant", "The user who authorized the client can no longer sign in."), null)
            : (null, user);
    }

    /// <summary>Rotates a client's refresh token and issues a new access token with it.</summary>
    private async Task<TokenOutcome> Refresh(IFormCollection form, string clientId, string issuer, string resource, CancellationToken cancellationToken)
    {
        var refreshToken = form["refresh_token"].ToString();
        if (refreshToken.Length == 0)
            return Refused("invalid_request", "refresh_token is required.");

        // The user is checked before the token rotates, so refusing a disabled user changes nothing: re-enabled, the
        // client's token still works. Reuse is answered as an ordinary miss, as the web app's refresh does.
        var rotation = await _refreshTokenStore.RotateForClient(
            clientId,
            refreshToken,
            async (userId, ct) => await FindUsableUser(userId, ct) is not null,
            cancellationToken);
        if (rotation.Outcome is not RefreshRotationOutcome.Rotated)
            return Refused("invalid_grant", "The refresh token is invalid or expired, or its user can no longer sign in.");

        return IssueAccessToken(rotation.UserId!, clientId, rotation.SessionId!.Value, issuer, resource, rotation.Token!);
    }

    /// <summary>
    /// The user, when they may still sign in: active, and still linked to their login provider. Checked on every
    /// issuance, as the web app's refresh does, so disabling a user stops their connectors at the next refresh.
    /// </summary>
    private async Task<ApplicationUser?> FindUsableUser(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
            return null;

        return await _userIdentityStore.ExistsActive(user.Id, user.LoginProvider, cancellationToken) ? user : null;
    }

    /// <summary>
    /// Handles a redemption that lost a race to another for the same code: whichever request won, the code was
    /// presented twice. The tracked changes are discarded, since they include the losing request's unsaved session.
    /// </summary>
    private async Task RevokeAfterLostRace(Guid authorizationId, CancellationToken cancellationToken)
    {
        _db.ChangeTracker.Clear();
        var current = await _db.OAuthAuthorizations.AsNoTracking().FirstAsync(a => a.Id == authorizationId, cancellationToken);
        await RevokeReplayedSession(current, cancellationToken);
    }

    /// <summary>
    /// Ends the session a replayed code opened: either holder of the code may be a thief, and the tokens it already
    /// issued are the only thing a thief would have gained (RFC 6749 §4.1.2). Its access tokens stop working with it.
    /// </summary>
    private async Task RevokeReplayedSession(OAuthAuthorization authorization, CancellationToken cancellationToken)
    {
        _logger.LogWarning("An authorization code for client {ClientId} was presented after it had been redeemed.", authorization.ClientId);

        if (authorization.SessionId is { } sessionId && authorization.UserId is { } userId)
            await _refreshTokenStore.Revoke(userId, sessionId, UserRefreshTokenRevokeReasons.AuthorizationCodeReused, cancellationToken);
    }

    /// <summary>
    /// Issues an access token for <paramref name="userId"/> bound to <paramref name="resource"/>, naming the session it
    /// belongs to so it stops working when that session is revoked.
    /// </summary>
    private TokenOutcome IssueAccessToken(string userId, string clientId, Guid sessionId, string issuer, string resource, string refreshToken)
    {
        var settings = OAuthAccessTokens.ReadSettings(_config);
        var now = _dateTimeProvider.Now;
        var lifetime = Duration.FromMinutes(settings.TokenExpirationInMinutes);

        var token = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            TokenType = OAuthAccessTokens.TokenType,
            Issuer = issuer,
            Audience = resource,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(OAuthAccessTokens.ClientIdClaim, clientId),
                new Claim(OAuthAccessTokens.SessionIdClaim, sessionId.ToString("N")),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
            IssuedAt = now.ToDateTimeUtc(),
            NotBefore = now.ToDateTimeUtc(),
            Expires = now.Plus(lifetime).ToDateTimeUtc(),
            SigningCredentials = new SigningCredentials(Local.ConfigureServices.CreateSigningKey(settings.Secret), SecurityAlgorithms.HmacSha256),
        });

        return new TokenOutcome.Issued(token, (int)lifetime.TotalSeconds, refreshToken);
    }

    /// <summary>The request <paramref name="handle"/> names, while it is still waiting for a decision; otherwise null.</summary>
    private async Task<OAuthAuthorization?> FindPendingRow(string handle, bool tracked, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(handle))
            return null;

        var hash = OAuthSecrets.Hash(handle);
        var now = _dateTimeProvider.Now;
        var rows = tracked ? _db.OAuthAuthorizations : _db.OAuthAuthorizations.AsNoTracking();
        return await rows.FirstOrDefaultAsync(
            a => a.RequestHandleHash == hash && a.Status == OAuthAuthorizationStatus.Pending && a.ExpiresAt > now,
            cancellationToken);
    }

    /// <summary>Saves, answering false when another request changed the row's status first.</summary>
    private async Task<bool> TrySave(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    /// <summary>Deletes requests that expired more than <see cref="_retention"/> ago; nothing reads them, and anyone can create them.</summary>
    private async Task PurgeExpired(Instant now, CancellationToken cancellationToken)
    {
        var cutoff = now.Minus(_retention);
        await _db.OAuthAuthorizations.Where(a => a.ExpiresAt < cutoff).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>The client's redirect URI carrying an authorization error (RFC 6749 §4.1.2.1) and the issuer (RFC 9207).</summary>
    private static string ErrorRedirect(string redirectUri, string error, string description, string? state, string issuer)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["error"] = error,
            ["error_description"] = description,
        };
        if (state is not null)
            parameters["state"] = state;
        parameters["iss"] = issuer;

        return QueryHelpers.AddQueryString(redirectUri, parameters);
    }

    /// <summary>A token endpoint refusal (RFC 6749 §5.2).</summary>
    private static TokenOutcome.Refused Refused(string error, string description) => new(error, description);

    /// <summary>Reads a parameter that must appear exactly once and not be empty.</summary>
    private static bool TrySingle(IQueryCollection query, string name, out string value)
    {
        StringValues values = query[name];
        value = values.Count == 1 ? values[0] ?? string.Empty : string.Empty;
        return value.Length > 0;
    }
}
