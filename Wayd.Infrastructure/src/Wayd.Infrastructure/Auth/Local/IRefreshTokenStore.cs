using Wayd.Infrastructure.Identity;

namespace Wayd.Infrastructure.Auth.Local;

/// <summary>
/// Owns the lifecycle of a user's refresh-token sessions: one row per concurrent sign-in.
/// </summary>
internal interface IRefreshTokenStore : IScopedService
{
    /// <summary>
    /// Starts a new session and returns its plaintext token. The token is returned here and
    /// nowhere else — only its hash is stored.
    /// </summary>
    Task<string> Issue(string userId, SessionContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies a presented token for the user and, on success, rotates it — returning the
    /// replacement. The outcome distinguishes an ordinary miss from detected reuse.
    /// </summary>
    Task<RefreshRotationResult> Rotate(string userId, string presentedToken, CancellationToken cancellationToken);

    /// <summary>
    /// Starts a session for an OAuth client acting for the user, and returns it with its plaintext token. Each
    /// authorization opens its own session, so the same client connected from two machines keeps both.
    /// </summary>
    /// <param name="sessionId">The new session's id, chosen by the caller so it can record the id in the same save.</param>
    /// <param name="userId">The user the client acts for.</param>
    /// <param name="clientId">The client's <c>client_id</c>; only that client may refresh the session.</param>
    /// <param name="clientName">The client's cleaned display name, shown in the user's sessions list.</param>
    /// <param name="context">The request that redeemed the authorization code.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <remarks>Its save commits whatever else the context is tracking, together with the session.</remarks>
    Task<ClientSession> IssueForClient(Guid sessionId, string userId, string clientId, string clientName, SessionContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies a refresh token presented by <paramref name="clientId"/> and, when <paramref name="mayRefresh"/>
    /// allows the session's user, rotates it. Reuse of a superseded token revokes the session, as <see cref="Rotate"/>
    /// does. A refused user leaves the session untouched, so the token works again once they may sign in.
    /// </summary>
    Task<ClientRotationResult> RotateForClient(
        string clientId,
        string presentedToken,
        Func<string, CancellationToken, Task<bool>> mayRefresh,
        CancellationToken cancellationToken);

    /// <summary>Live sessions for the user, newest first.</summary>
    Task<IReadOnlyList<UserRefreshToken>> ListActive(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the session a presented refresh token belongs to, or null. Used to mark "this
    /// device" in the sessions list and to scope sign-out to the calling session.
    /// </summary>
    Task<Guid?> FindSessionId(string userId, string presentedToken, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes one session. Returns false when the id is not a live session for this user —
    /// callers must not reveal whether the id existed.
    /// </summary>
    Task<bool> Revoke(string userId, Guid sessionId, string reason, CancellationToken cancellationToken);

    /// <summary>Revokes every live session for the user. Idempotent.</summary>
    Task RevokeAll(string userId, string reason, CancellationToken cancellationToken);
}

/// <summary>
/// Request-derived detail recorded on a session so a person can recognise it later. Both
/// values are client-influenced and used only for display.
/// </summary>
internal readonly record struct SessionContext(string? UserAgent, string? IpAddress)
{
    public static SessionContext None => new(null, null);
}

internal enum RefreshRotationOutcome
{
    /// <summary>The token matched a live session, which has been rotated.</summary>
    Rotated,

    /// <summary>No live session matched. Indistinguishable from a token that never existed.</summary>
    NotFound,

    /// <summary>
    /// The token matched a session's superseded value: two parties hold tokens from one
    /// chain. That session is revoked; the user's other sessions are untouched.
    /// </summary>
    ReuseDetected,
}

/// <summary>A session opened for an OAuth client.</summary>
/// <param name="SessionId">The session's id, recorded on the authorization so a replayed code can revoke it.</param>
/// <param name="Token">The plaintext refresh token, returned here and nowhere else.</param>
internal readonly record struct ClientSession(Guid SessionId, string Token);

/// <summary>The outcome of rotating an OAuth client's refresh token.</summary>
/// <param name="Outcome">Whether the token was rotated, unknown, or a replay.</param>
/// <param name="Token">The replacement token when rotated.</param>
/// <param name="UserId">The user the session acts for when rotated.</param>
/// <param name="SessionId">The session rotated, which the new access token names.</param>
internal readonly record struct ClientRotationResult(RefreshRotationOutcome Outcome, string? Token, string? UserId, Guid? SessionId)
{
    /// <summary>No live session for the client holds the token, or its user may not refresh.</summary>
    public static ClientRotationResult NotFound() => new(RefreshRotationOutcome.NotFound, null, null, null);
}

internal readonly record struct RefreshRotationResult(RefreshRotationOutcome Outcome, string? Token)
{
    public static RefreshRotationResult Rotated(string token) => new(RefreshRotationOutcome.Rotated, token);
    public static RefreshRotationResult NotFound() => new(RefreshRotationOutcome.NotFound, null);
    public static RefreshRotationResult ReuseDetected() => new(RefreshRotationOutcome.ReuseDetected, null);
}
