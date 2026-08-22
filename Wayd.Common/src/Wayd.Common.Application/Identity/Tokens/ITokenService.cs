namespace Wayd.Common.Application.Identity.Tokens;

public interface ITokenService : ITransientService
{
    Task<TokenResponse> GetTokenAsync(LoginCommand command, CancellationToken cancellationToken);
    Task<TokenResponse> RefreshTokenAsync(RefreshTokenCommand command, CancellationToken cancellationToken);
    Task<TokenResponse> ExchangeTokenAsync(ExchangeTokenCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Ends the calling device's session server-side. Idempotent. The caller must pass the
    /// authenticated principal's own id — never a value taken from the request body.
    /// <paramref name="refreshToken"/> identifies which session to end. When it is absent or
    /// matches no live session, nothing is revoked — the caller is still signed out locally,
    /// and revoking every session on an unidentifiable token would end sessions the user did
    /// not ask to end.
    /// </summary>
    Task LogoutAsync(string userId, string? refreshToken, CancellationToken cancellationToken);

    /// <summary>Ends every session for the user — "sign out everywhere".</summary>
    Task LogoutAllAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// The user's live sessions, newest-used first. <paramref name="currentRefreshToken"/> is
    /// optional and only marks which row is the caller's own device.
    /// </summary>
    Task<IReadOnlyList<UserSessionResponse>> GetSessions(string userId, string? currentRefreshToken, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes one of the user's sessions. Returns false when the id is not a live session for
    /// this user; callers must not distinguish that from "not yours".
    /// </summary>
    Task<bool> RevokeSession(string userId, Guid sessionId, CancellationToken cancellationToken);
    Task<AuthProvidersResponse> GetAuthProviders(CancellationToken cancellationToken);
}
