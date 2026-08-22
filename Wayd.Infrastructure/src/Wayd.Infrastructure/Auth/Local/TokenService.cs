using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.OidcProviders;
using Wayd.Common.Application.Identity.Tokens;
using Wayd.Infrastructure.Auth.Oidc;

namespace Wayd.Infrastructure.Auth.Local;

internal class TokenService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IConfiguration config,
    IDateTimeProvider dateTimeProvider,
    IUserIdentityStore userIdentityStore,
    IUserService userService,
    IOidcTokenValidator oidcTokenValidator,
    IOidcProviderRegistry oidcProviderRegistry,
    IRefreshTokenStore refreshTokenStore,
    ISessionContextAccessor sessionContext,
    ILogger<TokenService> logger) : ITokenService
{
    private readonly IRefreshTokenStore _refreshTokenStore = refreshTokenStore;
    private readonly ISessionContextAccessor _sessionContext = sessionContext;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly IConfiguration _config = config;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IUserIdentityStore _userIdentityStore = userIdentityStore;
    private readonly IUserService _userService = userService;
    private readonly IOidcTokenValidator _oidcTokenValidator = oidcTokenValidator;
    private readonly IOidcProviderRegistry _oidcProviderRegistry = oidcProviderRegistry;
    private readonly ILogger<TokenService> _logger = logger;

    public async Task<TokenResponse> GetTokenAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByNameAsync(command.UserName);
        if (user is null)
        {
            _logger.LogWarning("Login failed: user {UserName} not found.", command.UserName);
            throw new UnauthorizedException("Invalid credentials.");
        }

        if (user.LoginProvider != LoginProviders.Wayd)
        {
            _logger.LogWarning("Login failed: user {UserName} is not a Wayd account (provider: {LoginProvider}).", command.UserName, user.LoginProvider);
            throw new UnauthorizedException("Invalid credentials.");
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: true);
        if (signInResult.IsLockedOut)
        {
            _logger.LogWarning("Login failed: user {UserName} is locked out.", command.UserName);
            throw new UnauthorizedException("Account is locked due to multiple failed login attempts. Please try again later.");
        }

        if (!signInResult.Succeeded)
        {
            _logger.LogWarning("Login failed: invalid password for user {UserName}.", command.UserName);
            throw new UnauthorizedException("Invalid credentials.");
        }

        // Check inactive status only after credentials are validated
        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed: user {UserName} is inactive.", command.UserName);
            throw new UnauthorizedException("Your account has been deactivated. Please contact an administrator.");
        }

        await EnsureActiveIdentityAsync(user, LoginProviders.Wayd, command.UserName, cancellationToken);

        return await GenerateTokensAndUpdateUser(user, cancellationToken);
    }

    public async Task<TokenResponse> RefreshTokenAsync(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var userPrincipal = GetPrincipalFromExpiredToken(command.Token);
        var userId = userPrincipal.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedException("Invalid token.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            throw new UnauthorizedException("Invalid token.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedException("User account is inactive.");
        }

        var rotation = await _refreshTokenStore.Rotate(user.Id, command.RefreshToken, cancellationToken);
        if (rotation.Outcome is not RefreshRotationOutcome.Rotated)
        {
            // Reuse is reported identically to an ordinary miss. Saying "this token was
            // already used" would confirm to a thief that they hold a real token from a real
            // chain; the store has already revoked that session either way.
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        // Deactivating a UserIdentity must also stop in-flight sessions, not just new
        // logins. Without this check a user whose identity was revoked could keep
        // minting fresh access tokens via refresh until the refresh-token TTL
        // (days) elapsed. Provider is whatever the user is currently linked to —
        // an Entra-exchanged user requires an active Entra identity; a local user
        // requires an active Wayd identity.
        await EnsureActiveIdentityAsync(user, user.LoginProvider, user.UserName ?? userId, cancellationToken);

        return await IssueAccessToken(user, rotation.Token!, cancellationToken);
    }

    public async Task<TokenResponse> ExchangeTokenAsync(ExchangeTokenCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Provider))
        {
            throw new UnauthorizedException("Invalid token.");
        }

        // The validator owns provider lookup, enabled-checks, and signature
        // validation. Unknown or disabled providers throw UnauthorizedException
        // there — no special-case for Entra config here anymore.
        var principal = await _oidcTokenValidator.Validate(command.Provider, command.SubjectToken, cancellationToken);

        string userId;
        if (string.Equals(command.Provider, LoginProviders.MicrosoftEntraId, StringComparison.Ordinal))
        {
            // Entra path: handles UserIdentity lookup, null-tid upgrade, new-user
            // creation, first-user-is-admin, and pending tenant migration.
            (userId, _) = await _userService.GetOrCreateFromPrincipalAsync(principal);
        }
        else
        {
            // GenericOidc path: resolves existing identities and applies pending
            // cross-provider migrations. Full new-user provisioning is a follow-up
            // (tracked separately) — unknown users are rejected explicitly here
            // rather than silently mis-provisioned.
            var resolved = await _userService.ResolveFromGenericOidcPrincipalAsync(
                command.Provider, principal, cancellationToken);

            if (resolved is null)
            {
                _logger.LogWarning(
                    "Token exchange rejected: provider {Provider} token validated but no matching user found. " +
                    "New-user provisioning for non-Entra providers is not yet implemented.",
                    command.Provider);
                throw new UnauthorizedException("No account found for this identity. Contact an administrator.");
            }

            userId = resolved.Value.Id;
        }

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new UnauthorizedException("Invalid token.");

        if (!user.IsActive)
        {
            _logger.LogWarning("Exchange failed: user {UserId} is inactive.", user.Id);
            throw new UnauthorizedException("Your account has been deactivated. Please contact an administrator.");
        }

        // Identity check is keyed by the actual provider from the request — same
        // value the validator just confirmed and the same value already written
        // into UserIdentity.Provider for this user.
        await EnsureActiveIdentityAsync(user, command.Provider, user.UserName ?? user.Id, cancellationToken);

        return await GenerateTokensAndUpdateUser(user, cancellationToken);
    }

    public async Task LogoutAsync(string userId, string? refreshToken, CancellationToken cancellationToken)
    {
        // Signs out the calling device only, leaving the user's other sessions alone.
        //
        // An unusable token revokes NOTHING rather than falling back to revoking everything.
        // "I cannot tell which session this is" is not the same as "end them all": treating it
        // that way once made a single sign-out destroy every session the user had. The caller
        // is signed out locally either way, and the orphaned session still expires on its own.
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning(
                "Logout for user {UserId} supplied no refresh token; no session could be identified to revoke.",
                userId);
            return;
        }

        var sessionId = await _refreshTokenStore.FindSessionId(userId, refreshToken, cancellationToken);
        if (sessionId is null)
        {
            _logger.LogWarning(
                "Logout for user {UserId} supplied a refresh token matching no live session; nothing revoked.",
                userId);
            return;
        }

        await _refreshTokenStore.Revoke(userId, sessionId.Value, UserRefreshTokenRevokeReasons.SignedOut, cancellationToken);
    }

    public async Task LogoutAllAsync(string userId, CancellationToken cancellationToken)
    {
        await _refreshTokenStore.RevokeAll(userId, UserRefreshTokenRevokeReasons.SignedOut, cancellationToken);
    }

    public async Task<IReadOnlyList<UserSessionResponse>> GetSessions(string userId, string? currentRefreshToken, CancellationToken cancellationToken)
    {
        var sessions = await _refreshTokenStore.ListActive(userId, cancellationToken);

        // Marking the caller's own row lets the UI warn before someone revokes the session
        // they are using. Null when the client did not send its token — the list still works,
        // it just cannot highlight "this device".
        var currentId = string.IsNullOrWhiteSpace(currentRefreshToken)
            ? null
            : await _refreshTokenStore.FindSessionId(userId, currentRefreshToken, cancellationToken);

        return sessions
            .Select(s => new UserSessionResponse(
                s.Id,
                s.DeviceLabel,
                s.IpAddress,
                s.CreatedAt,
                s.LastUsedAt,
                s.ExpiresAt,
                s.Id == currentId))
            .ToList();
    }

    public async Task<bool> RevokeSession(string userId, Guid sessionId, CancellationToken cancellationToken)
    {
        return await _refreshTokenStore.Revoke(userId, sessionId, UserRefreshTokenRevokeReasons.SignedOut, cancellationToken);
    }

    private async Task EnsureActiveIdentityAsync(ApplicationUser user, string provider, string usernameForLogging, CancellationToken cancellationToken)
    {
        // Requires an active UserIdentity row for the given provider. Enables
        // "disable login for this user" by deactivating the identity row — no new
        // flag needed. Applied on login, refresh, and exchange so revocation takes
        // effect on the user's next refresh, not when the refresh token expires.
        var hasActiveIdentity = await _userIdentityStore.ExistsActive(user.Id, provider, cancellationToken);
        if (!hasActiveIdentity)
        {
            _logger.LogWarning("Authentication failed: user {UserName} has no active {Provider} identity.", usernameForLogging, provider);
            throw new UnauthorizedException("Invalid credentials.");
        }
    }

    private async Task<TokenResponse> GenerateTokensAndUpdateUser(ApplicationUser user, CancellationToken cancellationToken)
    {
        var settings = GetSettings();

        // Permissions are embedded in the JWT as claims so the frontend doesn't
        // need a separate /permissions fetch on load. Re-read on every issuance
        // (including refresh), so an admin permission change takes effect on the
        // user's next refresh — no version tracking needed, TTL is the revocation
        // clock.
        var permissions = await _userService.GetPermissionsAsync(user.Id, cancellationToken);

        // A fresh sign-in opens its own session rather than replacing whatever else the user
        // has open, so signing in on a second device leaves the first one working.
        var refreshToken = await _refreshTokenStore.Issue(user.Id, _sessionContext.Current, cancellationToken);

        return BuildResponse(user, permissions, settings, refreshToken);
    }

    /// <summary>
    /// Mints an access token against an already-rotated refresh token, for the refresh path
    /// where the store owns the session row.
    /// </summary>
    private async Task<TokenResponse> IssueAccessToken(ApplicationUser user, string refreshToken, CancellationToken cancellationToken)
    {
        var settings = GetSettings();
        var permissions = await _userService.GetPermissionsAsync(user.Id, cancellationToken);

        return BuildResponse(user, permissions, settings, refreshToken);
    }

    private TokenResponse BuildResponse(
        ApplicationUser user,
        IReadOnlyList<string> permissions,
        LocalJwtSettings settings,
        string refreshToken)
    {
        var token = GenerateJwt(user, permissions, settings);
        var tokenExpiry = _dateTimeProvider.Now.ToDateTimeUtc().AddMinutes(settings.TokenExpirationInMinutes);

        return new TokenResponse(token, refreshToken, tokenExpiry, user.MustChangePassword);
    }

    private string GenerateJwt(ApplicationUser user, IReadOnlyList<string> permissions, LocalJwtSettings settings)
    {
        var key = ConfigureServices.CreateSigningKey(settings.Secret);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FirstName ?? string.Empty),
            new(ClaimTypes.Surname, user.LastName ?? string.Empty),
            // Frontend reads this to drive provider-specific UX (e.g. showing the
            // "Change Password" button only for local users) and to gate the
            // forced-password-change flow. Without it, authMethod is null for
            // every session and those branches silently disable themselves.
            new("loginProvider", user.LoginProvider),
        };

        if (user.EmployeeId.HasValue)
        {
            claims.Add(new Claim("EmployeeId", user.EmployeeId.Value.ToString()));
        }

        // One claim per permission (ASP.NET Core idiom). Enables both
        // ClaimsPrincipal.HasClaim("permission", ...) on the server and a
        // uniform token shape across all login providers.
        foreach (var permission in permissions)
        {
            claims.Add(new Claim(ApplicationClaims.Permission, permission));
        }

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: _dateTimeProvider.Now.ToDateTimeUtc().AddMinutes(settings.TokenExpirationInMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        var settings = GetSettings();

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = ConfigureServices.CreateSigningKey(settings.Secret),
            ValidateLifetime = false, // Allow expired tokens for refresh
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

        if (securityToken is not JwtSecurityToken jwtToken ||
            !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new UnauthorizedException("Invalid token.");
        }

        return principal;
    }

    private LocalJwtSettings GetSettings()
    {
        var settings = _config.GetSection(LocalJwtSettings.SectionName).Get<LocalJwtSettings>();
        if (settings is null || string.IsNullOrWhiteSpace(settings.Secret))
        {
            throw new InvalidOperationException("Local JWT settings are not configured.");
        }

        return settings;
    }

    public async Task<AuthProvidersResponse> GetAuthProviders(CancellationToken cancellationToken)
    {
        // Local username/password is always available. Wayd doesn't currently
        // support disabling it — every deployment can mint Wayd-local accounts
        // even when SSO is the primary path. If that changes, this is the spot
        // to gate it on a configuration flag.
        var oidcProviders = await _oidcProviderRegistry.GetEnabled(cancellationToken);

        var infos = oidcProviders
            .Select(p => new OidcProviderInfo(
                Name: p.Name,
                DisplayName: p.DisplayName,
                ProviderType: p.ProviderType.ToString(),
                Authority: p.Authority,
                ClientId: p.ClientId,
                Scopes: p.Scopes))
            .ToList();

        return new AuthProvidersResponse(Local: true, Oidc: infos);
    }
}
