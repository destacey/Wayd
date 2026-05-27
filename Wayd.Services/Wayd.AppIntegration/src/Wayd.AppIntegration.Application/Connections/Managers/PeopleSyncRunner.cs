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
    IUserService userService) : IPeopleSyncRunner
{
    private readonly ILogger<PeopleSyncRunner> _logger = logger;
    private readonly ISender _sender = sender;
    private readonly IAppIntegrationDbContext _db = db;
    private readonly IDateTimeProvider _clock = clock;
    private readonly IEntraEmployeeSource _entraEmployeeSource = entraEmployeeSource;
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
                _logger.LogInformation("No active people-sync connections found.");
                return Result.Failure("No active people-sync connections found.");
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

        return await RunConnection(connectionId, connection.Connector, trigger, cancellationToken);
    }

    private async Task<Result> RunConnection(Guid connectionId, Connector connector, SyncTriggerSource trigger, CancellationToken cancellationToken)
    {
        // SyncType.Full is the only meaningful mode for people sync today; the upsert command is
        // idempotent and always reconciles against the full set returned by the source.
        var run = SyncRun.Start(connectionId, connector, SyncType.Full, trigger, _clock.Now);
        _db.SyncRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        var details = new PeopleSyncDetail();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fetchResult = await FetchEmployees(connectionId, connector, cancellationToken);
            if (fetchResult.IsFailure)
            {
                details.Errors.Add(fetchResult.Error);
                run.MarkFailed(_clock.Now, fetchResult.Error);
                await SaveRun(run, details, cancellationToken);
                return Result.Failure(fetchResult.Error);
            }

            var employees = fetchResult.Value.ToList();
            details.EmployeesFetched = employees.Count;

            if (employees.Count == 0)
            {
                var msg = "Source returned zero employees.";
                details.Errors.Add(msg);
                run.MarkFailed(_clock.Now, msg);
                await SaveRun(run, details, cancellationToken);
                return Result.Failure(msg);
            }

            var upsertResult = await _sender.Send(new BulkUpsertEmployeesCommand(employees), cancellationToken);
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
        CancellationToken cancellationToken)
    {
        // Routed by connector. Add new connectors (Workday, BambooHR, ...) by adding arms here.
        return connector switch
        {
            Connector.Entra => await FetchFromEntra(connectionId, cancellationToken),
            _ => Result.Failure<IEnumerable<Wayd.Common.Application.Interfaces.IExternalEmployee>>(
                $"No people source registered for connector '{connector}'.")
        };
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

    // Match the API-wide convention so frontend consumers read camelCase keys
    // (the rest of the API uses MVC's camelCase JsonNamingPolicy; this direct
    // Serialize call needs to be told explicitly).
    private static readonly JsonSerializerOptions _detailsJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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
        public List<string> Errors { get; set; } = [];
    }
}
