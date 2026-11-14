using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.Auth.Local;

internal sealed class RefreshTokenStore(
    WaydDbContext db,
    IDateTimeProvider dateTimeProvider,
    IConfiguration config,
    ILogger<RefreshTokenStore> logger) : IRefreshTokenStore
{
    /// <summary>
    /// How long after a rotation the superseded token is still taken for the caller's own retry rather than replay:
    /// two tabs refreshing at once, or a token read just before a background refresh replaced it. Within it, and from
    /// the address that rotated, the old token is refused without ending the session; from anywhere else, or after
    /// it, presenting the old token revokes the session. Sign-out and the sessions list resolve it within the window
    /// from any address.
    /// </summary>
    private static readonly Duration RetryGrace = Duration.FromSeconds(10);

    private readonly WaydDbContext _db = db;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IConfiguration _config = config;
    private readonly ILogger<RefreshTokenStore> _logger = logger;

    public async Task<string> Issue(string userId, string credentialVersion, SessionContext context, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var token = GenerateToken();

        _db.UserRefreshTokens.Add(new UserRefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = RefreshTokenHasher.Hash(token),
            ExpiresAt = FirstExpiry(now),
            CredentialVersion = credentialVersion,
            DeviceLabel = DeviceLabelParser.Parse(context.UserAgent),
            IpAddress = Truncate(context.IpAddress, IpAddressMaxLength),
            CreatedAt = now,
            LastUsedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return token;
    }

    public async Task<IReadOnlyList<UserRefreshToken>> ListActive(string userId, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var openedAfter = now.Minus(Duration.FromDays(Settings.SessionLifetimeInDays));
        return await _db.UserRefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now && t.CreatedAt > openedAfter)
            .OrderByDescending(t => t.LastUsedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid?> FindSessionId(string userId, string presentedToken, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;

        var sessions = await _db.UserRefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var current = sessions.FirstOrDefault(t => RefreshTokenHasher.Verify(presentedToken, t.TokenHash));
        if (current is not null)
        {
            return current.Id;
        }

        // Also accept the token this session rotated away from within RetryGrace, the window in which Rotate takes it
        // for the caller's own retry. A background refresh can land between the client reading its stored token and
        // using it, and the two lookups must agree on what identifies a session — otherwise a caller holding a
        // seconds-old token looks like a stranger.
        return sessions.FirstOrDefault(t => IsWithinRetryGrace(t, now) && RefreshTokenHasher.Verify(presentedToken, t.PreviousTokenHash))?.Id;
    }

    public async Task<bool> Revoke(string userId, Guid sessionId, string reason, CancellationToken cancellationToken)
    {
        // Scoped by user id as well as session id: a session id from another account must
        // read as "not found" rather than revoking someone else's session.
        var session = await _db.UserRefreshTokens
            .FirstOrDefaultAsync(t => t.Id == sessionId && t.UserId == userId && t.RevokedAt == null, cancellationToken);

        if (session is null)
        {
            return false;
        }

        await RevokeAndSave(session, reason, cancellationToken);

        return true;
    }

    public async Task<RefreshRotationResult> Rotate(string userId, string presentedToken, string credentialVersion, SessionContext context, CancellationToken cancellationToken)
    {
        // Scoped to one user, so this is a handful of rows — one per device. There is no
        // equality lookup to index against: every hash carries its own salt, so the only way
        // to find the match is to verify each candidate. An OAuth client's session is never a
        // candidate: it is refreshed only by that client, through the token endpoint.
        var sessions = await _db.UserRefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ClientId == null)
            .ToListAsync(cancellationToken);

        return await RotateAmong(sessions, presentedToken, userId, credentialVersion, context, _ => GenerateToken(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ClientSession> IssueForClient(
        Guid sessionId,
        string userId,
        string clientId,
        string clientName,
        TokenScope scope,
        string credentialVersion,
        SessionContext context,
        CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var token = GenerateClientToken(sessionId);

        _db.UserRefreshTokens.Add(new UserRefreshToken
        {
            Id = sessionId,
            UserId = userId,
            ClientId = clientId,
            Scope = scope,
            TokenHash = RefreshTokenHasher.Hash(token),
            ExpiresAt = FirstExpiry(now),
            CredentialVersion = credentialVersion,
            DeviceLabel = Truncate(clientName, DeviceLabelMaxLength),
            IpAddress = Truncate(context.IpAddress, IpAddressMaxLength),
            CreatedAt = now,
            LastUsedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new ClientSession(sessionId, token);
    }

    /// <inheritdoc />
    public async Task<ClientRotationResult> RotateForClient(
        string clientId,
        string presentedToken,
        Func<string, CancellationToken, Task<string?>> credentialVersionOf,
        SessionContext context,
        CancellationToken cancellationToken)
    {
        if (!TryReadSessionId(presentedToken, out var sessionId))
        {
            return ClientRotationResult.NotFound();
        }

        // The client is compared here rather than in the query: the column's collation is case-insensitive, and a
        // client_id is a URL whose path is not.
        var session = await _db.UserRefreshTokens
            .FirstOrDefaultAsync(t => t.Id == sessionId && t.RevokedAt == null, cancellationToken);
        if (session is null || !string.Equals(session.ClientId, clientId, StringComparison.Ordinal))
        {
            return ClientRotationResult.NotFound();
        }

        // A refused user leaves the session untouched, so only the current token's holder asks; a replay is answered
        // whoever the user is.
        string? credentialVersion = null;
        var holdsCurrentToken = IsLive(session, _dateTimeProvider.Now) && RefreshTokenHasher.Verify(presentedToken, session.TokenHash);
        if (holdsCurrentToken && (credentialVersion = await credentialVersionOf(session.UserId, cancellationToken)) is null)
        {
            return ClientRotationResult.NotFound();
        }

        var result = await RotateAmong([session], presentedToken, session.UserId, credentialVersion, context, s => GenerateClientToken(s.Id), cancellationToken);
        return result.Outcome is RefreshRotationOutcome.Rotated
            ? new ClientRotationResult(result.Outcome, result.Token, session.UserId, session.Id, session.Scope ?? TokenScopes.Default)
            : new ClientRotationResult(result.Outcome, null, null, null, null);
    }

    /// <summary>
    /// Rotates whichever of <paramref name="sessions"/> holds <paramref name="presentedToken"/>, or revokes the one
    /// whose superseded token it is.
    /// </summary>
    private async Task<RefreshRotationResult> RotateAmong(
        IReadOnlyList<UserRefreshToken> sessions,
        string presentedToken,
        string userId,
        string? credentialVersion,
        SessionContext context,
        Func<UserRefreshToken, string> generateToken,
        CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;

        var session = sessions.FirstOrDefault(t => RefreshTokenHasher.Verify(presentedToken, t.TokenHash));
        if (session is not null)
        {
            if (!IsLive(session, now))
            {
                return RefreshRotationResult.NotFound();
            }

            // Opened by a sign-in that checked a password the user has since replaced, after that change ended the
            // user's sessions: nothing else stops it, so it ends here.
            if (session.CredentialVersion is { } openedUnder && openedUnder != credentialVersion)
            {
                _logger.LogWarning("Refresh refused for user {UserId}: the session was opened under credentials that have since changed. Revoking it.", userId);
                await RevokeAndSave(session, UserRefreshTokenRevokeReasons.CredentialChanged, cancellationToken);
                return RefreshRotationResult.NotFound();
            }

            var replacement = generateToken(session);
            var end = EndOf(session);

            session.PreviousTokenHash = session.TokenHash;
            session.PreviousTokenExpiresAt = end;
            session.TokenHash = RefreshTokenHasher.Hash(replacement);
            session.ExpiresAt = Min(now.Plus(Duration.FromDays(RefreshTokenExpirationInDays)), end);
            session.LastUsedAt = now;
            session.RotatedFromIpAddress = Truncate(context.IpAddress, IpAddressMaxLength);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another request rotated or revoked the session since it was read. Its token is the one that counts;
                // this caller's is now the superseded one, which is its own retry within RetryGrace.
                _db.Entry(session).State = EntityState.Detached;
                return RefreshRotationResult.NotFound();
            }

            return RefreshRotationResult.Rotated(replacement);
        }

        // No live token matched. If the value matches the one a session rotated away from, two
        // parties hold tokens from the same chain and there is no way to tell which is the
        // attacker — so that session dies. Only that one: the user's other devices are
        // unaffected, which is the whole reason sessions are rows rather than columns. The
        // previous token is recognised for the session's whole life, so a holder left behind by
        // one rotation is caught however long it waits. A token two or more rotations old
        // matches nothing and is refused as unknown: it gains nothing, but is not flagged.
        var reused = sessions.FirstOrDefault(t =>
            t.PreviousTokenExpiresAt is { } previousExpiry
            && previousExpiry > now
            && RefreshTokenHasher.Verify(presentedToken, t.PreviousTokenHash));

        if (reused is null)
        {
            return RefreshRotationResult.NotFound();
        }

        // Only from where the rotation came: otherwise whoever holds a stolen token could rotate just before the
        // client's scheduled refresh and have the client's own presentation of the old token pass as a retry.
        if (IsWithinRetryGrace(reused, now) && Truncate(context.IpAddress, IpAddressMaxLength) == reused.RotatedFromIpAddress)
        {
            return RefreshRotationResult.NotFound();
        }

        _logger.LogWarning(
            "Refresh token reuse detected for user {UserId}: a superseded token was presented. Revoking that session.",
            userId);

        await RevokeAndSave(reused, UserRefreshTokenRevokeReasons.ReuseDetected, cancellationToken);

        return RefreshRotationResult.ReuseDetected();
    }

    public async Task RevokeAll(string userId, string reason, CancellationToken cancellationToken)
    {
        // A rotation committed between the read and the save fails the save on the row version. The revocation is
        // what must hold, so the rows are read again and revoked as they are now.
        for (var attempt = 1; ; attempt++)
        {
            var sessions = await _db.UserRefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ToListAsync(cancellationToken);

            if (sessions.Count == 0)
            {
                return;
            }

            var now = _dateTimeProvider.Now;
            foreach (var session in sessions)
            {
                Revoke(session, now, reason);
            }

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < MaxRevokeAttempts)
            {
                foreach (var entry in ex.Entries)
                    await entry.ReloadAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// Revokes <paramref name="session"/> and saves it. A rotation committed since it was read fails the save on the
    /// row version; the revocation is what must hold, so the row is read again and revoked as it is now, unless
    /// something else revoked it meanwhile.
    /// </summary>
    private async Task RevokeAndSave(UserRefreshToken session, string reason, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            Revoke(session, _dateTimeProvider.Now, reason);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxRevokeAttempts)
            {
                await _db.Entry(session).ReloadAsync(cancellationToken);
                if (session.RevokedAt is not null)
                    return;
            }
        }
    }

    /// <summary>How many times a revocation is tried against rows a concurrent rotation keeps changing before it fails.</summary>
    private const int MaxRevokeAttempts = 3;

    private static void Revoke(UserRefreshToken session, Instant now, string reason)
    {
        session.RevokedAt = now;
        session.RevokedReason = reason;

        // Clear both halves of the chain. Leaving the previous hash behind would let a revoked
        // session be resurrected through the reuse window.
        session.PreviousTokenHash = null;
        session.PreviousTokenExpiresAt = null;
    }

    /// <summary>Whether <paramref name="session"/>'s token may still be refreshed: neither expired nor past the session's end.</summary>
    private bool IsLive(UserRefreshToken session, Instant now) => session.ExpiresAt > now && EndOf(session) > now;

    /// <summary>Whether <paramref name="session"/> rotated so recently that its superseded token is taken for a retry.</summary>
    private static bool IsWithinRetryGrace(UserRefreshToken session, Instant now) =>
        session.PreviousTokenHash is not null && now - session.LastUsedAt <= RetryGrace;

    /// <summary>When <paramref name="session"/> ends however often it refreshes: <see cref="LocalJwtSettings.SessionLifetimeInDays"/> after it opened.</summary>
    private Instant EndOf(UserRefreshToken session) => session.CreatedAt.Plus(Duration.FromDays(Settings.SessionLifetimeInDays));

    /// <summary>The first token's expiry for a session opened at <paramref name="now"/>.</summary>
    private Instant FirstExpiry(Instant now) =>
        Min(now.Plus(Duration.FromDays(RefreshTokenExpirationInDays)), now.Plus(Duration.FromDays(Settings.SessionLifetimeInDays)));

    /// <summary>The earlier of two instants.</summary>
    private static Instant Min(Instant a, Instant b) => a < b ? a : b;

    /// <summary>The configured session settings, or their defaults.</summary>
    private LocalJwtSettings Settings =>
        _config.GetSection(LocalJwtSettings.SectionName).Get<LocalJwtSettings>() ?? new LocalJwtSettings();

    private int RefreshTokenExpirationInDays => Settings.RefreshTokenExpirationInDays;

    private static string GenerateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    /// <summary>
    /// A client's token names its session, <c>{sessionId}.{secret}</c>: the token endpoint is not told the user,
    /// so it cannot narrow the search the way the web app's refresh does, and the salted hashes cannot be looked up
    /// by equality. The id only locates the row; the secret is still verified against its hash.
    /// </summary>
    private static string GenerateClientToken(Guid sessionId) =>
        $"{sessionId:N}.{Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32))}";

    /// <summary>Reads the session id a client's token begins with; false when the token does not have that form.</summary>
    private static bool TryReadSessionId(string presentedToken, out Guid sessionId)
    {
        sessionId = Guid.Empty;
        var separator = presentedToken.IndexOf('.');
        return separator > 0 && Guid.TryParseExact(presentedToken.AsSpan(0, separator), "N", out sessionId);
    }

    private const int IpAddressMaxLength = 45;

    private const int DeviceLabelMaxLength = 200;

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
