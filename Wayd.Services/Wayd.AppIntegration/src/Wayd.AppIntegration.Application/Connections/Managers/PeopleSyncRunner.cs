using System.Diagnostics;
using System.Text.Json;
using MediatR;
using Wayd.AppIntegration.Application.Interfaces;
using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Enums;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;

namespace Wayd.AppIntegration.Application.Connections.Managers;

/// <summary>
/// Per-run orchestrator for people sync. Loops over all active connections with the People
/// capability, resolves each connector's <see cref="IEmployeeSource"/> via the keyed factory, and
/// feeds the fetched employees into the existing <see cref="BulkUpsertEmployeesCommand"/>
/// pipeline. The runner never names a concrete connector — adding a people connector means
/// registering a source and a descriptor builder, not editing this class. Persists one
/// <see cref="SyncRun"/> row per connection per run; per-run metrics are stuffed into
/// <c>DetailsJson</c> since the SyncRun schema is currently work-item-flavored.
/// </summary>
public sealed class PeopleSyncRunner(
    ILogger<PeopleSyncRunner> logger,
    ISender sender,
    IAppIntegrationDbContext db,
    IDateTimeProvider clock,
    IEmployeeSourceFactory sourceFactory,
    IEnumerable<ISyncableConnectionDescriptorBuilder> descriptorBuilders,
    IUserService userService) : IPeopleSyncRunner
{
    private readonly ILogger<PeopleSyncRunner> _logger = logger;
    private readonly ISender _sender = sender;
    private readonly IAppIntegrationDbContext _db = db;
    private readonly IDateTimeProvider _clock = clock;
    private readonly IEmployeeSourceFactory _sourceFactory = sourceFactory;
    private readonly IReadOnlyDictionary<Connector, ISyncableConnectionDescriptorBuilder> _descriptorBuilders =
        descriptorBuilders.ToDictionary(b => b.Connector);
    private readonly IUserService _userService = userService;

    public async Task<Result> Run(SyncTriggerSource trigger, SyncType requestedSyncType, CancellationToken cancellationToken)
    {
        var syncId = Guid.CreateVersion7();
        using (_logger.BeginScope(new Dictionary<string, object> { ["SyncId"] = syncId }))
        {
            _logger.LogInformation("PeopleSyncRunner starting (trigger={Trigger}, requestedSyncType={SyncType})", trigger, requestedSyncType);

            // Load all non-deleted connections and filter by capability + CanSync. CanSync lives
            // on ISyncableConnection and encodes IsActive && IsValidConfiguration &&
            // HasActiveIntegrationObjects — the same predicate WorkSyncRunner uses, so both
            // runners agree on what "ready to sync" means.
            var connections = await _db.Connections
                .Where(c => !c.IsDeleted)
                .ToListAsync(cancellationToken);

            var active = connections
                .Where(c => c.Connector.HasCapability(ConnectorCapability.People)
                            && c is ISyncableConnection syncable
                            && syncable.CanSync)
                .ToList();

            if (active.Count == 0)
            {
                // No-op runs aren't failures. Scheduled "run all" jobs fire on a cadence
                // independent of whether any connections exist or are active; returning
                // Failure here would trip Hangfire's AutomaticRetry and create noise without
                // ever changing the outcome (still no connections to sync next time).
                _logger.LogInformation("No active people-sync connections found.");
                return Result.Success();
            }

            // Defense in depth: the Activate command enforces single-active PeopleSync at write
            // time. If two ever slip through (concurrent activation, direct DB tampering,
            // historical data) we'd risk one source deactivating the other's employees on every
            // run. Refuse the whole run loudly rather than silently picking one.
            if (active.Count > 1)
            {
                var names = string.Join(", ", active.Select(c => $"{c.Name} ({c.Connector})"));
                _logger.LogError("Aborting people-sync run: {Count} active PeopleSync connections found, expected at most 1. Active: {Names}.", active.Count, names);
                return Result.Failure($"Multiple active PeopleSync connections found ({names}). Only one may be active at a time.");
            }

            int successCount = 0;
            foreach (var connection in active)
            {
                using (_logger.BeginScope(new Dictionary<string, object> { ["ConnectionId"] = connection.Id }))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await RunConnection(connection.Id, connection.Connector, trigger, requestedSyncType, cancellationToken);
                    if (result.IsSuccess) successCount++;
                }
            }

            _logger.LogInformation("PeopleSyncRunner finished: {Succeeded}/{Total} connection runs succeeded.", successCount, active.Count);
            return Result.Success();
        }
    }

    public async Task<Result> Run(Guid connectionId, SyncTriggerSource trigger, SyncType requestedSyncType, CancellationToken cancellationToken)
    {
        var connection = await _db.Connections
            .FirstOrDefaultAsync(c => c.Id == connectionId && !c.IsDeleted, cancellationToken);
        if (connection is null)
            return Result.Failure($"Connection {connectionId} not found.");

        if (!connection.Connector.HasCapability(ConnectorCapability.People))
            return Result.Failure($"Connection {connectionId} is not a people-sync connection.");

        // Inactive connections must never sync. The controller blocks this at request time;
        // this guard catches any caller that bypasses the controller (recurring Hangfire jobs
        // scheduled with a stale connectionId, direct invocation, etc.).
        if (!connection.IsActive)
            return Result.Failure($"Connection {connectionId} is inactive.");

        return await RunConnection(connectionId, connection.Connector, trigger, requestedSyncType, cancellationToken);
    }

    private async Task<Result> RunConnection(Guid connectionId, Connector connector, SyncTriggerSource trigger, SyncType requestedSyncType, CancellationToken cancellationToken)
    {
        // Resolve the connector's source up front: descriptor builder (typed entity load, boxed
        // config) → keyed IEmployeeSource → Bind. Resolution failures still produce a failed
        // SyncRun row so the sync-history audit trail shows why nothing happened.
        var sourceResult = await ResolveSource(connectionId, connector, cancellationToken);
        if (sourceResult.IsFailure)
        {
            var failedRun = SyncRun.Start(connectionId, connector, SyncType.Full, trigger, _clock.Now);
            _db.SyncRuns.Add(failedRun);
            failedRun.MarkFailed(_clock.Now, sourceResult.Error);
            await SaveRun(failedRun, new PeopleSyncDetail { Errors = [sourceResult.Error] }, cancellationToken);
            return Result.Failure(sourceResult.Error);
        }
        var source = sourceResult.Value;

        // The caller picks the sync type. Manual/full and scheduled/full ignore any prior watermark
        // and re-pull everything. Differential reads the most recent successful run's FinishedAt as
        // the watermark — first run with no prior success silently degrades to a full snapshot, since
        // "incremental from null" is meaningless.
        Instant? lastSuccessfulRunAt = null;
        if (requestedSyncType == SyncType.Differential && source.SupportsIncremental)
        {
            lastSuccessfulRunAt = await _db.SyncRuns
                .Where(r => r.ConnectionId == connectionId && r.Status == SyncRunStatus.Succeeded && r.FinishedAt != null)
                .OrderByDescending(r => r.FinishedAt)
                .Select(r => r.FinishedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var syncType = lastSuccessfulRunAt is not null ? SyncType.Differential : SyncType.Full;
        var incremental = syncType == SyncType.Differential;

        var run = SyncRun.Start(connectionId, connector, syncType, trigger, _clock.Now);
        _db.SyncRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        var details = new PeopleSyncDetail();

        // Per-stage wall-clock timing. The connection run spans four sequential stages (fetch →
        // upsert → user-link → user-sync); any of them could dominate the total. Timing each one
        // tells us where to invest rather than optimizing the wrong stage on a hunch.
        var stageTimer = Stopwatch.StartNew();
        long fetchMs = 0, upsertMs = 0, userLinkMs = 0, userSyncMs = 0;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            stageTimer.Restart();
            var fetchResult = await source.GetEmployees(lastSuccessfulRunAt, cancellationToken);
            fetchMs = stageTimer.ElapsedMilliseconds;
            if (fetchResult.IsFailure)
            {
                details.Errors.Add(fetchResult.Error);
                run.MarkFailed(_clock.Now, fetchResult.Error);
                await SaveRun(run, details, cancellationToken);
                return Result.Failure(fetchResult.Error);
            }

            var employees = fetchResult.Value.Employees.ToList();
            var exclusionCounts = fetchResult.Value.ExclusionCounts;
            details.EmployeesFetched = employees.Count;
            details.IncrementalUsed = incremental;
            if (exclusionCounts.Count > 0)
            {
                details.EmployeesExcluded = exclusionCounts.Sum(c => c.Count);
                details.ExclusionBreakdown = [.. exclusionCounts
                    .Select(c => new ExclusionBreakdownEntry(
                        OrgTypeId: c.RuleType,
                        OrgReference: c.RuleReference,
                        DisplayName: c.DisplayName,
                        Count: c.Count))];
            }

            // Incremental syncs only return changed records, so a missing employee doesn't mean
            // "no longer exists" — it means "no changes". Skip the deactivation pass on those runs
            // and let the next full sync reconcile.
            if (employees.Count == 0)
            {
                if (incremental)
                {
                    // Zero changes since the last sync is a normal outcome, not a failure.
                    _logger.LogInformation("Incremental sync returned zero changed employees for connection {ConnectionId} — nothing to upsert.", connectionId);
                    run.MarkSucceeded(_clock.Now);
                    await SaveRun(run, details, cancellationToken);
                    return Result.Success();
                }

                var msg = "Source returned zero employees.";
                details.Errors.Add(msg);
                run.MarkFailed(_clock.Now, msg);
                await SaveRun(run, details, cancellationToken);
                return Result.Failure(msg);
            }

            stageTimer.Restart();
            var upsertResult = await _sender.Send(
                new BulkUpsertEmployeesCommand(employees, matchBy: source.MatchBy, deactivateMissing: !incremental),
                cancellationToken);
            upsertMs = stageTimer.ElapsedMilliseconds;
            if (upsertResult.IsFailure)
            {
                details.Errors.Add($"BulkUpsertEmployees failed: {upsertResult.Error}");
                run.MarkFailed(_clock.Now, upsertResult.Error);
                await SaveRun(run, details, cancellationToken);
                return Result.Failure(upsertResult.Error);
            }
            details.EmployeesUpserted = employees.Count;

            stageTimer.Restart();
            var userLinkResult = await _userService.UpdateMissingEmployeeIds(cancellationToken);
            userLinkMs = stageTimer.ElapsedMilliseconds;
            if (userLinkResult.IsFailure)
            {
                details.Errors.Add($"UpdateMissingEmployeeIds failed: {userLinkResult.Error}");
                run.RecordError();
            }

            stageTimer.Restart();
            var userUpdateResult = await _userService.SyncUsersFromEmployeeRecords(employees, cancellationToken);
            userSyncMs = stageTimer.ElapsedMilliseconds;
            if (userUpdateResult.IsFailure)
            {
                details.Errors.Add($"SyncUsersFromEmployeeRecords failed: {userUpdateResult.Error}");
                run.RecordError();
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "People sync stages for connection {ConnectionId} ({EmployeeCount} employees): fetch {FetchMs}ms, upsert {UpsertMs}ms, user-link {UserLinkMs}ms, user-sync {UserSyncMs}ms.",
                    connectionId, employees.Count, fetchMs, upsertMs, userLinkMs, userSyncMs);
            }

            run.MarkSucceeded(_clock.Now);
            await SaveRun(run, details, cancellationToken);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            run.MarkCancelled(_clock.Now);
            await SaveRun(run, details, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while running people sync for connection {ConnectionId}.", connectionId);
            details.Errors.Add(ex.Message);
            run.MarkFailed(_clock.Now, ex.Message);
            await SaveRun(run, details, CancellationToken.None);
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Resolves the bound <see cref="IEmployeeSource"/> for a connection: descriptor builder →
    /// keyed source → Bind. Each step returns a failure rather than throwing so the caller can
    /// record it on the sync run.
    /// </summary>
    private async Task<Result<IEmployeeSource>> ResolveSource(Guid connectionId, Connector connector, CancellationToken cancellationToken)
    {
        if (!_descriptorBuilders.TryGetValue(connector, out var builder))
            return Result.Failure<IEmployeeSource>($"No connection descriptor builder is registered for connector '{connector}'.");

        var descriptorResult = await builder.Build(connectionId, cancellationToken);
        if (descriptorResult.IsFailure)
            return Result.Failure<IEmployeeSource>(descriptorResult.Error);

        return _sourceFactory.Create(descriptorResult.Value);
    }

    // Match the API-wide convention so frontend consumers read camelCase keys
    // (the rest of the API uses MVC's camelCase JsonNamingPolicy; this direct
    // Serialize call needs to be told explicitly).
    private static readonly JsonSerializerOptions _detailsJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // SaveRun is bookkeeping for the sync-history audit trail. Failures here are deliberately
    // logged and swallowed rather than propagated:
    //
    //   * The sync itself has already succeeded or failed on its own merits — the runner records
    //     that outcome via the SyncRun.MarkSucceeded/Failed call before SaveRun runs. Throwing
    //     from SaveRun would surface a misleading error (the sync didn't fail; the history row did)
    //     and trip Hangfire's AutomaticRetry, which would re-run the whole sync and risk
    //     double-upserts.
    //   * A persistent DB failure here is observable: the Error-level log fires every run and
    //     the missing SyncRun row will be visible (or rather, conspicuously missing) in the
    //     sync history UI.
    //
    // Mirrors WorkSyncRunner.SaveRun. If we ever want sync-history persistence to be load-bearing
    // (e.g. a downstream alerting flow depends on a row existing), revisit then.
    private async Task SaveRun(SyncRun run, PeopleSyncDetail details, CancellationToken cancellationToken)
    {
        try
        {
            run.SetDetails(JsonSerializer.Serialize(details, _detailsJsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize people-sync details for SyncRun {SyncRunId}.", run.Id);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist SyncRun {SyncRunId} updates.", run.Id);
        }
    }

    /// <summary>
    /// Per-connection people-sync detail serialized to <see cref="SyncRun.DetailsJson"/>.
    /// Lives here (not on the SyncRun entity) because the entity's shape is work-item-flavored
    /// and we don't want to extend it for a single PeopleSync consumer yet.
    /// </summary>
    private sealed class PeopleSyncDetail
    {
        public int EmployeesFetched { get; set; }
        public int EmployeesUpserted { get; set; }

        /// <summary>
        /// Total records dropped by source-side exclusion rules, summed across all rules.
        /// Distinct from <see cref="EmployeesFetched"/> — these records came back from the source
        /// and were filtered before the upsert.
        /// </summary>
        public int EmployeesExcluded { get; set; }

        /// <summary>Per-rule breakdown of exclusions (only present when at least one rule fired).</summary>
        public List<ExclusionBreakdownEntry>? ExclusionBreakdown { get; set; }

        public bool IncrementalUsed { get; set; }
        public List<string> Errors { get; set; } = [];
    }

    /// <summary>
    /// One entry in the sync run's exclusion breakdown. Property names are part of the persisted
    /// DetailsJson contract — keep them stable.
    /// </summary>
    private sealed record ExclusionBreakdownEntry(
        string OrgTypeId,
        string OrgReference,
        string? DisplayName,
        int Count);
}
