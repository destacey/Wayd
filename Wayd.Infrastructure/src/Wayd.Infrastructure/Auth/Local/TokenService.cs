using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NodaTime;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.OidcProviders;
using Wayd.Common.Application.Identity.Tokens;
using Wayd.Common.Domain.Identity;
using Wayd.Infrastructure.Auth.Oidc;
using Wayd.Infrastructure.Auth.Permissions;

namespace Wayd.Infrastructure.Auth.Local;

internal class TokenService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<ApplicationRole> roleManager,
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
    private readonly RoleManager<ApplicationRole> _roleManager = roleManager;
    private readonly IConfiguration _config = config;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IUserIdentityStore _userIdentityStore = userIdentityStore;
    private readonly IUserService _userService = userService;
    private readonly IOidcTokenValidator _oidcTokenValidator = oidcTokenValidator;
    private readonly IOidcProviderRegistry _oidcProviderRegistry = oidcProviderRegistry;
    private readonly ILogger<TokenService> _logger = logger;

    /// <summary>
    /// Every sign-in refusal's message, whatever the reason: an unknown user, a wrong password, a locked, inactive or
    /// non-local account. One message, naming the lockout too, so the owner learns to wait while nobody learns
    /// whether the account exists, its state, or whether a guess made during a lockout was right.
    /// </summary>
    internal const string SignInRefused = "Invalid credentials, or the account is temporarily locked after too many attempts. Try again later.";

    /// <summary>The refusal for a temporary password past its lifetime, given only once the password is known to be right.</summary>
    internal const string TemporaryPasswordExpired = "Your temporary password has expired. Ask an administrator to set a new one.";

    public async Task<TokenResponse> GetTokenAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByNameAsync(command.UserName);
        if (user is null || user.LoginProvider != LoginProviders.Wayd)
        {
            // A hash is verified anyway, so an unknown or non-local user takes as long to refuse as a wrong password.
            BurnAPasswordCheck(command.Password);
            _logger.LogWarning("Login failed: user {UserName} not found or not a Wayd account.", command.UserName);
            throw new UnauthorizedException(SignInRefused);
        }

        var now = _dateTimeProvider.Now.ToDateTimeOffset();
        if (await LastAdministrator.IsProtected(_userManager, _roleManager, user, now))
        {
            await CheckProtectedAdministratorPassword(user, command);
        }
        else
        {
            var wasLockedOut = await _userManager.IsLockedOutAsync(user);
            var signInResult = await _signInManager.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: true);
            if (signInResult.IsLockedOut && !wasLockedOut)
            {
                await RecordLockout(user);
            }

            if (!signInResult.Succeeded)
            {
                // A locked-out account is refused before its hash is checked, so one is checked here, or locked
                // accounts would answer faster than any other.
                if (wasLockedOut)
                    BurnAPasswordCheck(command.Password);

                _logger.LogWarning("Login failed for user {UserName}: {Reason}.", command.UserName, signInResult.IsLockedOut ? "locked out" : "invalid password");
                throw new UnauthorizedException(SignInRefused);
            }
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed: user {UserName} is inactive.", command.UserName);
            throw new UnauthorizedException(SignInRefused);
        }

        if (HasExpiredTemporaryPassword(user))
        {
            _logger.LogWarning("Login refused: user {UserName}'s temporary password has expired.", command.UserName);
            throw new UnauthorizedException(TemporaryPasswordExpired);
        }

        await EnsureActiveIdentityAsync(user, LoginProviders.Wayd, command.UserName, cancellationToken);

        return await GenerateTokensAndUpdateUser(user, cancellationToken);
    }

    /// <summary>
    /// Checks the password of an administrator <see cref="LastAdministrator"/> protects from lockout. An attempt due
    /// later, or taken by another request, is refused without checking the password, so guesses cannot be sent faster
    /// than the throttle, nor in parallel.
    /// </summary>
    private async Task CheckProtectedAdministratorPassword(ApplicationUser user, LoginCommand command)
    {
        var now = _dateTimeProvider.Now.ToDateTimeOffset();
        if (!await LastAdministrator.TryTakeAttempt(_userManager, user, now))
        {
            BurnAPasswordCheck(command.Password);
            _logger.LogWarning("Login refused for user {UserName}, an administrator protected from lockout: the next attempt is not due yet.", command.UserName);
            throw new UnauthorizedException(SignInRefused);
        }

        if (!await _userManager.CheckPasswordAsync(user, command.Password))
        {
            await LastAdministrator.RecordFailure(_userManager, user, now);
            _logger.LogWarning("Login failed: invalid password for user {UserName}, an administrator protected from lockout.", command.UserName);
            throw new UnauthorizedException(SignInRefused);
        }

        await LastAdministrator.RecordSuccess(_userManager, user);
    }

    /// <summary>
    /// Whether <paramref name="user"/> still has to replace a temporary password set more than
    /// <see cref="LocalJwtSettings.TemporaryPasswordLifetimeInDays"/> ago. One set before the time was recorded has no
    /// recorded age and does not expire.
    /// </summary>
    private bool HasExpiredTemporaryPassword(ApplicationUser user) =>
        PasswordChangeRequirement.AppliesTo(user)
        && user.TemporaryPasswordSetAt is { } setAt
        && setAt.Plus(Duration.FromDays(GetSettings().TemporaryPasswordLifetimeInDays)) <= _dateTimeProvider.Now;

    /// <summary>
    /// Verifies <paramref name="password"/> against a hash of a random password no account holds, made once with the
    /// configured hasher so the check costs what a real one does.
    /// </summary>
    private void BurnAPasswordCheck(string password)
    {
        var hasher = _userManager.PasswordHasher;
        _unknownUserHash ??= hasher.HashPassword(_unknownUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        hasher.VerifyHashedPassword(_unknownUser, _unknownUserHash, password);
    }

    /// <summary>The account <see cref="BurnAPasswordCheck"/> hashes for, which exists nowhere.</summary>
    private static readonly ApplicationUser _unknownUser = new();

    /// <summary>The hash <see cref="BurnAPasswordCheck"/> verifies against.</summary>
    private static string? _unknownUserHash;

    /// <summary>
    /// Records the lockout this attempt triggered. The sign-in manager has already saved it, so this save carries
    /// only the event; a failure is logged rather than thrown, since the caller is refused either way.
    /// </summary>
    private async Task RecordLockout(ApplicationUser user)
    {
        if (user.LockoutEnd is null)
            return;

        user.RecordLockout(EventActor.System, _dateTimeProvider.Now);

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            user.ClearDomainEvents();
            _logger.LogError("Failed to record the lockout of user {UserId}: {Errors}",
                user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
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

        if (HasExpiredTemporaryPassword(user))
        {
            throw new UnauthorizedException(TemporaryPasswordExpired);
        }

        var rotation = await _refreshTokenStore.Rotate(user.Id, command.RefreshToken, CredentialVersion.Of(user.SecurityStamp), _sessionContext.Current, cancellationToken);
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
        var (principal, provider) = await _oidcTokenValidator.Validate(command.Provider, command.SubjectToken, cancellationToken);

        string userId;
        if (provider.ProviderType == OidcProviderType.MicrosoftEntraId)
        {
            // The Entra path writes and resolves identity rows under the fixed provider name, so an Entra provider
            // stored under any other name would sign its users in as that one.
            if (!string.Equals(provider.Name, LoginProviders.MicrosoftEntraId, StringComparison.Ordinal))
            {
                _logger.LogError(
                    "Token exchange rejected: Microsoft Entra ID provider is stored as '{Provider}', not '{Expected}'.",
                    provider.Name, LoginProviders.MicrosoftEntraId);
                throw new UnauthorizedException("Invalid token.");
            }

            // Entra path: handles UserIdentity lookup, null-tid upgrade, new-user
            // creation, first-user-is-admin, and pending tenant migration.
            (userId, _) = await _userService.GetOrCreateFromPrincipalAsync(principal);
        }
        else
        {
            // A generic provider under a reserved name would resolve and complete links meant for the Entra provider or
            // for local accounts, with no tenant check. The create rules refuse one; a row written before they did, or
            // by hand, is refused here.
            if (string.Equals(provider.Name?.Trim(), LoginProviders.MicrosoftEntraId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider.Name?.Trim(), LoginProviders.Wayd, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Token exchange rejected: generic OIDC provider is stored under the reserved name '{Provider}'.", provider.Name);
                throw new UnauthorizedException("Invalid token.");
            }

            // GenericOidc path: resolves existing identities and applies pending
            // cross-provider migrations. Full new-user provisioning is a follow-up
            // (tracked separately) — unknown users are rejected explicitly here
            // rather than silently mis-provisioned.
            var resolved = await _userService.ResolveFromGenericOidcPrincipalAsync(
                provider.Name, principal, cancellationToken);

            if (resolved is null)
            {
                _logger.LogWarning(
                    "Token exchange rejected: provider {Provider} token validated but no matching user found. " +
                    "New-user provisioning for non-Entra providers is not yet implemented.",
                    provider.Name);
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

        // The stored provider's name: the one written into UserIdentity.Provider at link time.
        await EnsureActiveIdentityAsync(user, provider.Name, user.UserName ?? user.Id, cancellationToken);

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
                s.Id == currentId,
                s.ClientId))
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
        // has open, so signing in on a second device leaves the first one working. The
        // credential version comes from the copy of the user the sign-in checked, so a password
        // changed while it ran leaves the session it opens unusable.
        var refreshToken = await _refreshTokenStore.Issue(user.Id, CredentialVersion.Of(user.SecurityStamp), _sessionContext.Current, cancellationToken);

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
            new(CredentialVersion.ClaimType, CredentialVersion.Of(user.SecurityStamp)),
        };

        // Restricts the token to replacing the password (PasswordChangeRequirement) until the user does.
        if (PasswordChangeRequirement.AppliesTo(user))
        {
            claims.Add(new Claim(PasswordChangeRequirement.ClaimType, "true"));
        }

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

            // Only the web app's own tokens; the access tokens Wayd's authorization server issues are typed at+jwt
            // and share this key.
            ValidTypes = ["JWT"],
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
