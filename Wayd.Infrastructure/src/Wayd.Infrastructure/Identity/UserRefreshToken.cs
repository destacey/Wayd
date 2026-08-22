using NodaTime;

namespace Wayd.Infrastructure.Identity;

/// <summary>
/// One sign-in session: a browser, a device, an app instance. A user has one row per
/// concurrent session, so signing in on a second device does not displace the first.
/// </summary>
/// <remarks>
/// Replaces the single RefreshToken column on <see cref="ApplicationUser"/>. That column
/// allowed exactly one live session per user — a second sign-in silently invalidated the
/// first at its next refresh — and made reuse detection global, so one replayed token
/// revoked every device the user had.
/// </remarks>
public class UserRefreshToken
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = null!;

    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Salted hash of the active token (<c>{salt}:{hash}</c>), never the token itself.
    /// Compare through <c>RefreshTokenHasher.Verify</c>, never by equality — the salt is
    /// per-hash, so the same token hashes differently every time.
    /// </summary>
    public string TokenHash { get; set; } = null!;

    public Instant ExpiresAt { get; set; }

    /// <summary>
    /// Hash of the token this row's current one replaced, retained only until
    /// <see cref="PreviousTokenExpiresAt"/> to make replay detectable. Never a credential.
    /// </summary>
    public string? PreviousTokenHash { get; set; }

    public Instant? PreviousTokenExpiresAt { get; set; }

    /// <summary>
    /// Coarse device description parsed from the User-Agent at sign-in ("Chrome on Windows"),
    /// so a person can tell their sessions apart. Null when the header was absent or
    /// unparseable — a session with no label is still revocable.
    /// </summary>
    public string? DeviceLabel { get; set; }

    /// <summary>
    /// Remote address at sign-in. Personal data: it exists so an unfamiliar session is
    /// recognisable, is never used for authorization, and is removed with the session row
    /// when the user is deleted.
    /// </summary>
    public string? IpAddress { get; set; }

    public Instant CreatedAt { get; set; }

    public Instant LastUsedAt { get; set; }

    /// <summary>
    /// Set when the session ends. Revoked rows are kept rather than deleted so a replayed
    /// token can be told apart from one that never existed, which is the difference between
    /// detecting theft and silently failing.
    /// </summary>
    public Instant? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }

    public bool IsActive => RevokedAt is null;
}

public static class UserRefreshTokenRevokeReasons
{
    public const string SignedOut = "SignedOut";
    public const string ReuseDetected = "ReuseDetected";
}
