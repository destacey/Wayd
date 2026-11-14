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
    /// Hash of the token this row's current one replaced, retained until
    /// <see cref="PreviousTokenExpiresAt"/>, the end of the session, so presenting it is recognised as replay for as
    /// long as the session lives. Never a credential.
    /// </summary>
    public string? PreviousTokenHash { get; set; }

    /// <summary>Until when <see cref="PreviousTokenHash"/> is recognised: the end of the session's lifetime.</summary>
    public Instant? PreviousTokenExpiresAt { get; set; }

    /// <summary>
    /// The user's credential version when the session was opened (<c>CredentialVersion</c>). The session refreshes
    /// only while the user's still matches, so one opened with a password that has since changed cannot be used.
    /// Null for a session opened before sessions recorded one, which is not checked.
    /// </summary>
    public string? CredentialVersion { get; set; }

    /// <summary>
    /// The remote address of the request that last rotated the session's token. Within the retry grace, the
    /// superseded token is taken for a retry only from this address. Personal data, kept for no other purpose.
    /// </summary>
    public string? RotatedFromIpAddress { get; set; }

    /// <summary>
    /// Changes on every write, so two rotations of the same token, or a rotation racing a revocation, cannot both
    /// commit.
    /// </summary>
    public byte[]? Version { get; set; }

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

    /// <summary>
    /// The OAuth client this session was opened for, by its <c>client_id</c>, or null for a sign-in to the web
    /// app. A client's session is refreshed only through the token endpoint and only by that client, and the web
    /// app's refresh never matches it.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// The scope the user granted the OAuth client, which every access token issued from this session carries. Null for
    /// a sign-in to the web app, which no scope limits, and for a client session opened before tokens had scopes,
    /// which keeps <see cref="TokenScopes.Default"/>.
    /// </summary>
    public TokenScope? Scope { get; set; }

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

    /// <summary>The authorization code that opened the session was presented again, so either holder may be a thief.</summary>
    public const string AuthorizationCodeReused = "AuthorizationCodeReused";

    /// <summary>The user changed their password, which ends the sessions the old one may have opened.</summary>
    public const string PasswordChanged = "PasswordChanged";

    /// <summary>An administrator set a new password for the user.</summary>
    public const string PasswordReset = "PasswordReset";

    /// <summary>The user was deactivated, by an administrator or by people sync.</summary>
    public const string Deactivated = "Deactivated";

    /// <summary>
    /// How the user signs in changed: converted to a local account, or rebound to another identity provider or tenant.
    /// A session opened through the old sign-in must not outlive it.
    /// </summary>
    public const string SignInChanged = "SignInChanged";

    /// <summary>
    /// The session was presented after the user's credentials changed under it: opened by a sign-in that checked a
    /// password the user has since replaced.
    /// </summary>
    public const string CredentialChanged = "CredentialChanged";
}
