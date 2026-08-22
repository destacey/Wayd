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
        var now = _dateTimeProvider.Now;

        // Scoped to one user, so this is a handful of rows — one per device. There is no
        // equality lookup to index against: every hash carries its own salt, so the only way
        // to find the match is to verify each candidate.
        var sessions = await _db.UserRefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        var session = sessions.FirstOrDefault(t => RefreshTokenHasher.Verify(presentedToken, t.TokenHash));
        if (session is not null)
        {
            if (session.ExpiresAt <= now)
            {
                return RefreshRotationResult.NotFound();
            }

            var replacement = GenerateToken();

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

    private const int IpAddressMaxLength = 45;

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
