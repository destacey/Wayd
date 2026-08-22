using Wayd.Infrastructure.Auth.Local;
using Wayd.Infrastructure.Identity;

namespace Wayd.Infrastructure.Tests.Sut.Auth.Local;

/// <summary>
/// In-memory <see cref="IRefreshTokenStore"/> modelling real session semantics: one entry per
/// sign-in, rotation on use, and reuse detection scoped to the session that was replayed.
/// </summary>
/// <remarks>
/// A Moq stub would let <see cref="TokenService"/> tests pass while asserting nothing about
/// sessions. The persistence behaviour this mirrors is covered against a real DbContext in
/// <c>RefreshTokenStoreTests</c>; this exists so token-issuance tests can still ask real
/// questions ("does a second sign-in leave the first working?") without a database.
/// </remarks>
internal sealed class FakeRefreshTokenStore : IRefreshTokenStore
{
    private sealed class Session
    {
        public Guid Id { get; } = Guid.NewGuid();
        public required string UserId { get; init; }
        public required string Token { get; set; }
        public string? PreviousToken { get; set; }
        public SessionContext Context { get; init; }
        public bool Revoked { get; set; }
        public string? RevokedReason { get; set; }
    }

    private readonly List<Session> _sessions = [];
    private int _counter;

    /// <summary>Live session count for a user — how many devices remain signed in.</summary>
    public int ActiveSessionCount(string userId) =>
        _sessions.Count(s => s.UserId == userId && !s.Revoked);

    public IReadOnlyList<string> RevokeReasons(string userId) =>
        _sessions.Where(s => s.UserId == userId && s.Revoked)
                 .Select(s => s.RevokedReason!)
                 .ToList();

    /// <summary>The context recorded when the session holding this token was opened.</summary>
    public SessionContext ContextFor(string token) =>
        _sessions.Single(s => s.Token == token).Context;

    /// <summary>Expires a session's token so the next rotation attempt fails on lifetime.</summary>
    public void ExpireToken(string token)
    {
        var session = _sessions.SingleOrDefault(s => s.Token == token)
            ?? throw new InvalidOperationException($"No session holds token '{token}'.");

        session.Token = $"expired:{session.Token}";
    }

    public Task<string> Issue(string userId, SessionContext context, CancellationToken cancellationToken)
    {
        var token = $"refresh-{++_counter}";
        _sessions.Add(new Session { UserId = userId, Token = token, Context = context });
        return Task.FromResult(token);
    }

    public Task<RefreshRotationResult> Rotate(string userId, string presentedToken, CancellationToken cancellationToken)
    {
        var live = _sessions.Where(s => s.UserId == userId && !s.Revoked).ToList();

        var match = live.FirstOrDefault(s => s.Token == presentedToken);
        if (match is not null)
        {
            var replacement = $"refresh-{++_counter}";
            match.PreviousToken = match.Token;
            match.Token = replacement;
            return Task.FromResult(RefreshRotationResult.Rotated(replacement));
        }

        var reused = live.FirstOrDefault(s => s.PreviousToken == presentedToken);
        if (reused is null)
        {
            return Task.FromResult(RefreshRotationResult.NotFound());
        }

        reused.Revoked = true;
        reused.RevokedReason = UserRefreshTokenRevokeReasons.ReuseDetected;
        reused.PreviousToken = null;
        return Task.FromResult(RefreshRotationResult.ReuseDetected());
    }

    public Task<IReadOnlyList<UserRefreshToken>> ListActive(string userId, CancellationToken cancellationToken)
    {
        IReadOnlyList<UserRefreshToken> rows = _sessions
            .Where(s => s.UserId == userId && !s.Revoked)
            .Select(s => new UserRefreshToken
            {
                Id = s.Id,
                UserId = s.UserId,
                TokenHash = s.Token,
                DeviceLabel = s.Context.UserAgent,
                IpAddress = s.Context.IpAddress,
            })
            .ToList();

        return Task.FromResult(rows);
    }

    public Task<Guid?> FindSessionId(string userId, string presentedToken, CancellationToken cancellationToken)
    {
        var match = _sessions.FirstOrDefault(s => s.UserId == userId && !s.Revoked && s.Token == presentedToken);
        return Task.FromResult(match?.Id);
    }

    public Task<bool> Revoke(string userId, Guid sessionId, string reason, CancellationToken cancellationToken)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == sessionId && s.UserId == userId && !s.Revoked);
        if (session is null)
        {
            return Task.FromResult(false);
        }

        session.Revoked = true;
        session.RevokedReason = reason;
        session.PreviousToken = null;
        return Task.FromResult(true);
    }

    public Task RevokeAll(string userId, string reason, CancellationToken cancellationToken)
    {
        foreach (var session in _sessions.Where(s => s.UserId == userId && !s.Revoked))
        {
            session.Revoked = true;
            session.RevokedReason = reason;
            session.PreviousToken = null;
        }

        return Task.CompletedTask;
    }
}
