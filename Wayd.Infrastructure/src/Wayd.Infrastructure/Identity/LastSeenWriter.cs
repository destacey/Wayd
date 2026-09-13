using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace Wayd.Infrastructure.Identity;

/// <summary>
/// Records when a user was last active and when a personal access token was last used, off the request path.
/// </summary>
/// <remarks>
/// Each subject is queued at most once per interval, so a burst of requests produces one write. The timestamps
/// are telemetry, so they are written with a conditional <c>ExecuteUpdate</c> that never moves one backwards
/// and deliberately bypasses <c>SaveChanges</c>: going through it would add an audit trail row and move
/// <c>SystemLastModified</c> on every sighting.
/// </remarks>
public sealed class LastSeenWriter : BackgroundService
{
    internal static readonly Duration UserActivityInterval = Duration.FromMinutes(30);
    internal static readonly Duration TokenUseInterval = Duration.FromHours(1);

    private const int DefaultCapacity = 1000;
    private const int MaxBatchSize = 100;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LastSeenWriter> _logger;
    private readonly Channel<Sighting> _channel;

    // Bounded by the number of users and tokens, not by traffic.
    private readonly ConcurrentDictionary<LastSeenKey, Instant> _queued = new();

    private int _dropped;

    public LastSeenWriter(IServiceScopeFactory scopeFactory, ILogger<LastSeenWriter> logger)
        : this(scopeFactory, logger, DefaultCapacity)
    {
    }

    internal LastSeenWriter(IServiceScopeFactory scopeFactory, ILogger<LastSeenWriter> logger, int capacity)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _channel = Channel.CreateBounded<Sighting>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            },
            OnDropped);
    }

    /// <returns>Whether the sighting was queued, rather than absorbed by an earlier one in the same interval.</returns>
    public bool RecordUserActivity(string userId, Instant at) =>
        Record(new LastSeenKey(LastSeenSubject.User, userId), at, UserActivityInterval);

    /// <returns>Whether the sighting was queued, rather than absorbed by an earlier one in the same interval.</returns>
    public bool RecordTokenUse(Guid tokenId, Instant at) =>
        Record(new LastSeenKey(LastSeenSubject.PersonalAccessToken, tokenId.ToString()), at, TokenUseInterval);

    private bool Record(LastSeenKey key, Instant at, Duration interval)
    {
        while (true)
        {
            if (_queued.TryGetValue(key, out var previous))
            {
                if (at - previous < interval)
                    return false;

                if (_queued.TryUpdate(key, at, previous))
                    break;
            }
            else if (_queued.TryAdd(key, at))
            {
                break;
            }
        }

        return _channel.Writer.TryWrite(new Sighting(key, at));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _channel.Reader;
        var batch = new Dictionary<LastSeenKey, Instant>();

        while (await reader.WaitToReadAsync(stoppingToken))
        {
            while (batch.Count < MaxBatchSize && reader.TryRead(out var sighting))
            {
                if (!batch.TryGetValue(sighting.Key, out var existing) || sighting.At > existing)
                    batch[sighting.Key] = sighting.At;
            }

            var dropped = Interlocked.Exchange(ref _dropped, 0);
            if (dropped > 0)
                _logger.LogWarning("Last-seen queue was full; dropped {Count} updates", dropped);

            try
            {
                await Write(batch, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // An exception escaping ExecuteAsync stops the host.
                foreach (var entry in batch)
                    _queued.TryRemove(entry);

                _logger.LogError(ex, "Failed to record a batch of {Count} last-seen updates", batch.Count);
            }
            finally
            {
                batch.Clear();
            }
        }
    }

    internal async Task Write(IReadOnlyDictionary<LastSeenKey, Instant> batch, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WaydDbContext>();

        foreach (var (key, at) in batch)
        {
            try
            {
                await (key.Subject switch
                {
                    LastSeenSubject.User => UpdateUserLastActivity(dbContext, key.Id, at, cancellationToken),
                    LastSeenSubject.PersonalAccessToken => UpdateTokenLastUsed(dbContext, Guid.Parse(key.Id), at, cancellationToken),
                    _ => throw new ArgumentOutOfRangeException(nameof(batch), key.Subject, "Unknown last-seen subject."),
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Release the throttle so the next sighting retries instead of waiting out the interval.
                _queued.TryRemove(KeyValuePair.Create(key, at));
                _logger.LogError(ex, "Failed to record last seen for {Subject} {Id}", key.Subject, key.Id);
            }
        }
    }

    private static Task<int> UpdateUserLastActivity(WaydDbContext dbContext, string userId, Instant at, CancellationToken cancellationToken) =>
        dbContext.Users
            .Where(u => u.Id == userId && (u.LastActivityAt == null || u.LastActivityAt < at))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastActivityAt, at), cancellationToken);

    private static Task<int> UpdateTokenLastUsed(WaydDbContext dbContext, Guid tokenId, Instant at, CancellationToken cancellationToken) =>
        dbContext.PersonalAccessTokens
            .Where(t => t.Id == tokenId && (t.LastUsedAt == null || t.LastUsedAt < at))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastUsedAt, at), cancellationToken);

    private void OnDropped(Sighting sighting)
    {
        // A dropped sighting would otherwise hold the throttle for a write that never happens.
        _queued.TryRemove(KeyValuePair.Create(sighting.Key, sighting.At));
        Interlocked.Increment(ref _dropped);
    }

    private readonly record struct Sighting(LastSeenKey Key, Instant At);
}

internal enum LastSeenSubject
{
    User,
    PersonalAccessToken,
}

internal readonly record struct LastSeenKey(LastSeenSubject Subject, string Id);
