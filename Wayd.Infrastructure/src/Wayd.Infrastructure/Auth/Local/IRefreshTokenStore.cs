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

internal readonly record struct RefreshRotationResult(RefreshRotationOutcome Outcome, string? Token)
{
    public static RefreshRotationResult Rotated(string token) => new(RefreshRotationOutcome.Rotated, token);
    public static RefreshRotationResult NotFound() => new(RefreshRotationOutcome.NotFound, null);
    public static RefreshRotationResult ReuseDetected() => new(RefreshRotationOutcome.ReuseDetected, null);
}
