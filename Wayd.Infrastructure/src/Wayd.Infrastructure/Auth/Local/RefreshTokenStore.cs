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
    /// How long a superseded token stays recognisable as a reuse signal. Raising this widens
    /// the window in which a client's own retry is read as an attack and revokes its session;
    /// it grants no additional access either way.
    /// </summary>
    private static readonly Duration ReuseDetectionWindow = Duration.FromMinutes(5);

    private readonly WaydDbContext _db = db;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IConfiguration _config = config;
    private readonly ILogger<RefreshTokenStore> _logger = logger;

    public async Task<string> Issue(string userId, SessionContext context, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var token = GenerateToken();

        _db.UserRefreshTokens.Add(new UserRefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = RefreshTokenHasher.Hash(token),
            ExpiresAt = now.Plus(Duration.FromDays(RefreshTokenExpirationInDays)),
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
        return await _db.UserRefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > _dateTimeProvider.Now)
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

        // Also accept the token this session just rotated away from, on the same terms as
        // Rotate's reuse check. A background refresh can land between the client reading its
        // stored token and using it, and the two lookups must agree on what identifies a
        // session — otherwise a caller holding a seconds-old token looks like a stranger.
        return sessions.FirstOrDefault(t =>
            t.PreviousTokenExpiresAt is { } previousExpiry
            && previousExpiry > now
            && RefreshTokenHasher.Verify(presentedToken, t.PreviousTokenHash))?.Id;
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

        Revoke(session, _dateTimeProvider.Now, reason);
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<RefreshRotationResult> Rotate(string userId, string presentedToken, CancellationToken cancellationToken)
    {
        // Scoped to one user, so this is a handful of rows — one per device. There is no
        // equality lookup to index against: every hash carries its own salt, so the only way
        // to find the match is to verify each candidate. An OAuth client's session is never a
        // candidate: it is refreshed only by that client, through the token endpoint.
        var sessions = await _db.UserRefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ClientId == null)
            .ToListAsync(cancellationToken);

        return await RotateAmong(sessions, presentedToken, userId, _ => GenerateToken(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ClientSession> IssueForClient(
        Guid sessionId,
        string userId,
        string clientId,
        string clientName,
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
            TokenHash = RefreshTokenHasher.Hash(token),
            ExpiresAt = now.Plus(Duration.FromDays(RefreshTokenExpirationInDays)),
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
        Func<string, CancellationToken, Task<bool>> mayRefresh,
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

        var holdsCurrentToken = session.ExpiresAt > _dateTimeProvider.Now && RefreshTokenHasher.Verify(presentedToken, session.TokenHash);
        if (holdsCurrentToken && !await mayRefresh(session.UserId, cancellationToken))
        {
            return ClientRotationResult.NotFound();
        }

        var result = await RotateAmong([session], presentedToken, session.UserId, s => GenerateClientToken(s.Id), cancellationToken);
        return result.Outcome is RefreshRotationOutcome.Rotated
            ? new ClientRotationResult(result.Outcome, result.Token, session.UserId, session.Id)
            : new ClientRotationResult(result.Outcome, null, null, null);
    }

    /// <summary>
    /// Rotates whichever of <paramref name="sessions"/> holds <paramref name="presentedToken"/>, or revokes the one
    /// whose superseded token it is.
    /// </summary>
    private async Task<RefreshRotationResult> RotateAmong(
        IReadOnlyList<UserRefreshToken> sessions,
        string presentedToken,
        string userId,
        Func<UserRefreshToken, string> generateToken,
        CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;

        var session = sessions.FirstOrDefault(t => RefreshTokenHasher.Verify(presentedToken, t.TokenHash));
        if (session is not null)
        {
            if (session.ExpiresAt <= now)
            {
                return RefreshRotationResult.NotFound();
            }

            var replacement = generateToken(session);

            session.PreviousTokenHash = session.TokenHash;
            session.PreviousTokenExpiresAt = now.Plus(ReuseDetectionWindow);
            session.TokenHash = RefreshTokenHasher.Hash(replacement);
            session.ExpiresAt = now.Plus(Duration.FromDays(RefreshTokenExpirationInDays));
            session.LastUsedAt = now;

            await _db.SaveChangesAsync(cancellationToken);

            return RefreshRotationResult.Rotated(replacement);
        }

        // No live token matched. If the value matches one this session already rotated away,
        // two parties hold tokens from the same chain and there is no way to tell which is the
        // attacker — so that session dies. Only that one: the user's other devices are
        // unaffected, which is the whole reason sessions are rows rather than columns.
        var reused = sessions.FirstOrDefault(t =>
            t.PreviousTokenExpiresAt is { } previousExpiry
            && previousExpiry > now
            && RefreshTokenHasher.Verify(presentedToken, t.PreviousTokenHash));

        if (reused is null)
        {
            return RefreshRotationResult.NotFound();
        }

        _logger.LogWarning(
            "Refresh token reuse detected for user {UserId}: a superseded token was presented. Revoking that session.",
            userId);

        Revoke(reused, now, UserRefreshTokenRevokeReasons.ReuseDetected);
        await _db.SaveChangesAsync(cancellationToken);

        return RefreshRotationResult.ReuseDetected();
    }

    public async Task RevokeAll(string userId, string reason, CancellationToken cancellationToken)
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

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void Revoke(UserRefreshToken session, Instant now, string reason)
    {
        session.RevokedAt = now;
        session.RevokedReason = reason;

        // Clear both halves of the chain. Leaving the previous hash behind would let a revoked
        // session be resurrected through the reuse window.
        session.PreviousTokenHash = null;
        session.PreviousTokenExpiresAt = null;
    }

    private int RefreshTokenExpirationInDays =>
        _config.GetSection(LocalJwtSettings.SectionName).Get<LocalJwtSettings>()?.RefreshTokenExpirationInDays
        ?? new LocalJwtSettings().RefreshTokenExpirationInDays;

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
