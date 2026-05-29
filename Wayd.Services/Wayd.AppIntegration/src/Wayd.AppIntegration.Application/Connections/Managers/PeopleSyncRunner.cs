using System.Text.Json;
using MediatR;
using Wayd.AppIntegration.Application.Interfaces;
using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Enums;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Application.Interfaces.ExternalPeople;

namespace Wayd.AppIntegration.Application.Connections.Managers;

/// <summary>
/// Per-run orchestrator for people sync. Loops over all active PeopleSync-category connections,
/// fetches employees from each source, and feeds them into the existing
/// <see cref="BulkUpsertEmployeesCommand"/> pipeline. Persists one <see cref="SyncRun"/> row per
/// connection per run; per-run metrics are stuffed into <c>DetailsJson</c> since the SyncRun
/// schema is currently work-item-flavored.
/// </summary>
public sealed class PeopleSyncRunner(
    ILogger<PeopleSyncRunner> logger,
    ISender sender,
    IAppIntegrationDbContext db,
    IDateTimeProvider clock,
    IEntraEmployeeSource entraEmployeeSource,
    IWorkdayEmployeeSource workdayEmployeeSource,
    IUserService userService) : IPeopleSyncRunner
{
    private readonly ILogger<PeopleSyncRunner> _logger = logger;
    private readonly ISender _sender = sender;
    private readonly IAppIntegrationDbContext _db = db;
    private readonly IDateTimeProvider _clock = clock;
    private readonly IEntraEmployeeSource _entraEmployeeSource = entraEmployeeSource;
    private readonly IWorkdayEmployeeSource _workdayEmployeeSource = workdayEmployeeSource;
    private readonly IUserService _userService = userService;

    public async Task<Result> Run(SyncTriggerSource trigger, CancellationToken cancellationToken)
    {
        var syncId = Guid.CreateVersion7();
        using (_logger.BeginScope(new Dictionary<string, object> { ["SyncId"] = syncId }))
        {
            _logger.LogInformation("PeopleSyncRunner starting (trigger={Trigger})", trigger);

            // Load all non-deleted connections and filter by category + CanSync. CanSync lives
            // on ISyncableConnection and encodes IsActive && IsValidConfiguration &&
            // HasActiveIntegrationObjects — the same predicate WorkSyncRunner uses, so both
            // runners agree on what "ready to sync" means.
            var connections = await _db.Connections
                .Where(c => !c.IsDeleted)
                .ToListAsync(cancellationToken);

            var active = connections
                .Where(c => c.Connector.GetCategory() == ConnectorCategory.PeopleSync
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
                    var result = await RunConnection(connection.Id, connection.Connector, trigger, cancellationToken);
                    if (result.IsSuccess) successCount++;
                }
            }

            _logger.LogInformation("PeopleSyncRunner finished: {Succeeded}/{Total} connection runs succeeded.", successCount, active.Count);
            return Result.Success();
        }
    }

    public async Task<Result> Run(Guid connectionId, SyncTriggerSource trigger, CancellationToken cancellationToken)
    {
        var connection = await _db.Connections
            .FirstOrDefaultAsync(c => c.Id == connectionId && !c.IsDeleted, cancellationToken);
        if (connection is null)
            return Result.Failure($"Connection {connectionId} not found.");

        if (connection.Connector.GetCategory() != ConnectorCategory.PeopleSync)
            return Result.Failure($"Connection {connectionId} is not a people-sync connection.");

        // Inactive connections must never sync. The controller blocks this at request time;
        // this guard catches any caller that bypasses the controller (recurring Hangfire jobs
        // scheduled with a stale connectionId, direct invocation, etc.).
        if (!connection.IsActive)
            return Result.Failure($"Connection {connectionId} is inactive.");

        return await RunConnection(connectionId, connection.Connector, trigger, cancellationToken);
    }

    private async Task<Result> RunConnection(Guid connectionId, Connector connector, SyncTriggerSource trigger, CancellationToken cancellationToken)
    {
        // Last-successful-run watermark for incremental connectors. Look it up before starting
        // the run so we can both feed it to the source AND tag the SyncRun with the right SyncType.
        var lastSuccessfulRunAt = await _db.SyncRuns
            .Where(r => r.ConnectionId == connectionId && r.Status == SyncRunStatus.Succeeded && r.FinishedAt != null)
            .OrderByDescending(r => r.FinishedAt)
            .Select(r => r.FinishedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var incremental = lastSuccessfulRunAt is not null && SourceSupportsIncremental(connector);
        var syncType = incremental ? SyncType.Differential : SyncType.Full;

        var run = SyncRun.Start(connectionId, connector, syncType, trigger, _clock.Now);
        _db.SyncRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        var details = new PeopleSyncDetail();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fetchResult = await FetchEmployees(connectionId, connector, lastSuccessfulRunAt, cancellationToken);
            if (fetchResult.IsFailure)
            {
                details.Errors.Add(fetchResult.Error);
                run.MarkFailed(_clock.Now, fetchResult.Error);
                await SaveRun(run, details, cancellationToken);
                return Result.Failure(fetchResult.Error);
            }

            var employees = fetchResult.Value.ToList();
            details.EmployeesFetched = employees.Count;
            details.IncrementalUsed = incremental;

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

            var matchBy = await ResolveMatchProperty(connectionId, connector, cancellationToken);

            var upsertResult = await _sender.Send(
                new BulkUpsertEmployeesCommand(employees, matchBy: matchBy, deactivateMissing: !incremental),
                cancellationToken);
            if (upsertResult.IsFailure)
            {
                details.Errors.Add($"BulkUpsertEmployees failed: {upsertResult.Error}");
                run.MarkFailed(_clock.Now, upsertResult.Error);
                await SaveRun(run, details, cancellationToken);
                return Result.Failure(upsertResult.Error);
            }
            details.EmployeesUpserted = employees.Count;

            var userLinkResult = await _userService.UpdateMissingEmployeeIds(cancellationToken);
            if (userLinkResult.IsFailure)
            {
                details.Errors.Add($"UpdateMissingEmployeeIds failed: {userLinkResult.Error}");
                run.RecordError();
            }

            var userUpdateResult = await _userService.SyncUsersFromEmployeeRecords(employees, cancellationToken);
            if (userUpdateResult.IsFailure)
            {
                details.Errors.Add($"SyncUsersFromEmployeeRecords failed: {userUpdateResult.Error}");
                run.RecordError();
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

    private async Task<Result<IEnumerable<Wayd.Common.Application.Interfaces.IExternalEmployee>>> FetchEmployees(
        Guid connectionId,
        Connector connector,
        Instant? lastSuccessfulRunAt,
        CancellationToken cancellationToken)
    {
        // Routed by connector. Add new connectors (BambooHR, ...) by adding arms here.
        return connector switch
        {
            Connector.Entra => await FetchFromEntra(connectionId, cancellationToken),
            Connector.Workday => await FetchFromWorkday(connectionId, lastSuccessfulRunAt, cancellationToken),
            _ => Result.Failure<IEnumerable<Wayd.Common.Application.Interfaces.IExternalEmployee>>(
                $"No people source registered for connector '{connector}'.")
        };
    }

    /// <summary>
    /// True when the source for <paramref name="connector"/> can return a delta of changed
    /// employees since a given timestamp. Used by the runner to (a) tag the SyncRun as
    /// Differential and (b) skip the deactivation pass on incremental runs.
    /// </summary>
    private static bool SourceSupportsIncremental(Connector connector) => connector switch
    {
        Connector.Workday => true,
        _ => false
    };

    /// <summary>
    /// Resolves the connection's <c>MatchBy</c> property for the BulkUpsert. Each PeopleSync
    /// connection type stores this on its own configuration; this dispatches by connector kind.
    /// </summary>
    private async Task<EmployeeMatchProperty> ResolveMatchProperty(Guid connectionId, Connector connector, CancellationToken cancellationToken)
    {
        switch (connector)
        {
            case Connector.Entra:
                var entra = await _db.EntraConnections.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
                return entra?.Configuration.MatchBy ?? EmployeeMatchProperty.Email;
            case Connector.Workday:
                var workday = await _db.WorkdayConnections.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
                return workday?.Configuration.MatchBy ?? EmployeeMatchProperty.Email;
            default:
                return EmployeeMatchProperty.Email;
        }
    }

    private async Task<Result<IEnumerable<Wayd.Common.Application.Interfaces.IExternalEmployee>>> FetchFromEntra(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        var entity = await _db.EntraConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
        if (entity is null)
            return Result.Failure<IEnumerable<Wayd.Common.Application.Interfaces.IExternalEmployee>>(
                $"Entra connection {connectionId} not found.");

        var credentials = new EntraConnectionCredentials(
            entity.Configuration.TenantId,
            entity.Configuration.ClientId,
            entity.Configuration.ClientSecret,
            entity.Configuration.AllUsersGroupObjectId,
            entity.Configuration.IncludeDisabledUsers);

        return await _entraEmployeeSource.GetEmployees(credentials, cancellationToken);
    }

    private async Task<Result<IEnumerable<Wayd.Common.Application.Interfaces.IExternalEmployee>>> FetchFromWorkday(
        Guid connectionId,
        Instant? lastSuccessfulRunAt,
        CancellationToken cancellationToken)
    {
        var entity = await _db.WorkdayConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
        if (entity is null)
            return Result.Failure<IEnumerable<Wayd.Common.Application.Interfaces.IExternalEmployee>>(
                $"Workday connection {connectionId} not found.");

        // Only pass the watermark if the admin opted into incremental sync. First-run case (no
        // prior successful run) falls back to a full snapshot regardless.
        var updatedFrom = entity.Configuration.IncrementalSyncEnabled ? lastSuccessfulRunAt : null;

        var credentials = new WorkdayConnectionCredentials(
            entity.Configuration.SoapEndpoint,
            entity.Configuration.TenantAlias,
            entity.Configuration.WsdlVersion,
            entity.Configuration.IsuUsername,
            entity.Configuration.IsuPassword,
            entity.Configuration.WorkerKey,
            entity.Configuration.IncludeInactive,
            updatedFrom);

        return await _workdayEmployeeSource.GetEmployees(credentials, cancellationToken);
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
        public bool IncrementalUsed { get; set; }
        public List<string> Errors { get; set; } = [];
    }
}
