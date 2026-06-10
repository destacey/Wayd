using System.Text.Json;
using MediatR;
using Wayd.AppIntegration.Application.Connections.Dtos;
using Wayd.AppIntegration.Application.Connections.Queries;
using Wayd.AppIntegration.Application.Interfaces;
using Wayd.Common.Application.Enums;
using Wayd.Common.Application.Logging;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;

namespace Wayd.AppIntegration.Application.Connections.Managers;

/// <summary>
/// Generic per-run sync orchestrator. Loops over all active syncable connections, resolves the
/// appropriate <see cref="IWorkItemSource"/> for each via the factory, and walks the source's
/// flat sync plan. Persists a <see cref="SyncRun"/> row per connection per run.
/// </summary>
public sealed class WorkSyncRunner(
    ILogger<WorkSyncRunner> logger,
    ISender sender,
    IWorkItemSourceFactory sourceFactory,
    IAppIntegrationDbContext db,
    IDateTimeProvider clock,
    IEnumerable<ISyncableConnectionDescriptorBuilder> descriptorBuilders) : IWorkSyncRunner
{
    private readonly ILogger<WorkSyncRunner> _logger = logger;
    private readonly ISender _sender = sender;
    private readonly IWorkItemSourceFactory _sourceFactory = sourceFactory;
    private readonly IAppIntegrationDbContext _db = db;
    private readonly IDateTimeProvider _clock = clock;
    private readonly IReadOnlyDictionary<Connector, ISyncableConnectionDescriptorBuilder> _descriptorBuilders = descriptorBuilders.ToDictionary(b => b.Connector);
    private static readonly Action<ILogger, Exception?> _runStarted = LoggerMessage.Define(LogLevel.Information,
        AppEventId.AppIntegration_WorkSyncRunner_RunStarted.ToEventId(),
        "WorkSyncRunner starting");

    private static readonly Action<ILogger, Exception?> _cancellationRequested = LoggerMessage.Define(LogLevel.Information,
        AppEventId.AppIntegration_CancellationRequested.ToEventId(),
        "Cancellation requested. Stopping sync.");

    private static readonly Action<ILogger, int, int, Exception?> _runSummary = LoggerMessage.Define<int, int>(LogLevel.Information,
        AppEventId.AppIntegration_WorkSyncRunner_RunSummary.ToEventId(),
        "WorkSyncRunner finished: {SucceededRuns}/{TotalRuns} connection runs succeeded.");

    public async Task<Result> Run(SyncType syncType, SyncTriggerSource trigger, CancellationToken cancellationToken)
    {
        _runStarted(_logger, null);

        var syncId = Guid.CreateVersion7();
        using (_logger.BeginScope(new Dictionary<string, object> { ["SyncId"] = syncId }))
        {
            try
            {
                var connections = await _sender.Send(
                    new GetConnectionsQuery(IncludeInactive: false, Capability: ConnectorCapability.WorkItems),
                    cancellationToken);
                var active = connections
                    .Where(c => c.CanSync == true)
                    .ToList();

                if (active.Count == 0)
                {
                    // No-op runs aren't failures. Scheduled "run all" jobs fire on a cadence
                    // independent of whether any connections exist or are active; returning
                    // Failure here would trip Hangfire's AutomaticRetry and create noise without
                    // ever changing the outcome (still no connections to sync next time).
                    _logger.LogInformation("No active syncable connections found.");
                    return Result.Success();
                }

                int successCount = 0;
                foreach (var connection in active)
                {
                    using (_logger.BeginScope(new Dictionary<string, object> { ["ConnectionId"] = connection.Id }))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var connectorEnum = (Connector)connection.Connector.Id;
                        var connectionResult = await RunConnection(connection.Id, connectorEnum, syncType, trigger, syncId, cancellationToken);
                        if (connectionResult.IsSuccess)
                            successCount++;
                    }
                }

                _runSummary(_logger, successCount, active.Count, null);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                _cancellationRequested(_logger, null);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WorkSyncRunner failed unexpectedly.");
                throw;
            }
        }
    }

    public async Task<Result> Run(Guid connectionId, SyncType syncType, SyncTriggerSource trigger, CancellationToken cancellationToken)
    {
        var syncId = Guid.CreateVersion7();
        using (_logger.BeginScope(new Dictionary<string, object> { ["SyncId"] = syncId, ["ConnectionId"] = connectionId }))
        {
            var connection = await _sender.Send(new GetConnectionQuery(connectionId), cancellationToken);
            if (connection is null)
                return Result.Failure($"Connection {connectionId} not found.");

            // Inactive connections must never sync. The controller blocks this at request time;
            // this guard catches any caller that bypasses the controller (recurring Hangfire jobs
            // scheduled with a stale connectionId, direct invocation, etc.).
            if (!connection.IsActive)
                return Result.Failure($"Connection {connectionId} is inactive.");

            var connectorEnum = (Connector)connection.Connector.Id;
            return await RunConnection(connectionId, connectorEnum, syncType, trigger, syncId, cancellationToken);
        }
    }

    private async Task<Result> RunConnection(Guid connectionId, Connector connector, SyncType syncType, SyncTriggerSource trigger, Guid syncId, CancellationToken cancellationToken)
    {
        // Load the typed entity + build a connector-neutral descriptor.
        var descriptorResult = await BuildDescriptor(connectionId, connector, cancellationToken);
        if (descriptorResult.IsFailure)
        {
            _logger.LogWarning("Skipping connection {ConnectionId}: {Error}", connectionId, descriptorResult.Error);
            return Result.Failure(descriptorResult.Error);
        }

        var sourceResult = _sourceFactory.Create(descriptorResult.Value);
        if (sourceResult.IsFailure)
        {
            _logger.LogWarning("Skipping connection {ConnectionId}: {Error}", connectionId, sourceResult.Error);
            return Result.Failure(sourceResult.Error);
        }

        var source = sourceResult.Value;

        var run = SyncRun.Start(connectionId, connector, syncType, trigger, _clock.Now);
        _db.SyncRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        var workspaceDetails = new List<WorkspaceSyncDetail>();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var refreshResult = await source.RefreshOrganizationConfiguration(syncId, cancellationToken);
            if (refreshResult.IsFailure)
            {
                run.MarkFailed(_clock.Now, $"RefreshOrganizationConfiguration failed: {refreshResult.Error}");
                await SaveRun(run, workspaceDetails, cancellationToken);
                return Result.Failure(refreshResult.Error);
            }

            var planResult = await source.GetSyncPlan(cancellationToken);
            if (planResult.IsFailure)
            {
                run.MarkFailed(_clock.Now, $"GetSyncPlan failed: {planResult.Error}");
                await SaveRun(run, workspaceDetails, cancellationToken);
                return Result.Failure(planResult.Error);
            }

            run.SetWorkspacesPlanned(planResult.Value.Count);

            if (descriptorResult.Value.SystemId is null)
            {
                run.MarkFailed(_clock.Now, "Connection has no SystemId.");
                await SaveRun(run, workspaceDetails, cancellationToken);
                return Result.Failure("Connection has no SystemId.");
            }

            foreach (var target in planResult.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (_logger.BeginScope(new Dictionary<string, object> { ["WorkspaceId"] = target.InternalWorkspaceId }))
                {
                    var detail = await RunWorkspace(source, target, descriptorResult.Value.SystemId!, syncType, syncId, cancellationToken);
                    workspaceDetails.Add(detail);

                    if (detail.Succeeded)
                    {
                        run.RecordWorkspaceSuccess(detail.WorkItemsProcessed);
                        // A workspace that succeeded overall but had a sub-step failure inside the
                        // source still counts as a degraded run — bump ErrorsCount so dashboards
                        // can flag it without parsing DetailsJson.
                        if (detail.HadPartialFailure)
                            run.RecordError();
                    }
                    else
                    {
                        run.RecordWorkspaceFailure();
                    }
                }
            }

            var dependenciesResult = await _sender.Send(new ProcessDependenciesCommand(descriptorResult.Value.SystemId!), cancellationToken);
            if (dependenciesResult.IsFailure)
            {
                _logger.LogError("ProcessDependencies failed for connection {ConnectionId}: {Error}", connectionId, dependenciesResult.Error);
                run.RecordError();
            }

            run.MarkSucceeded(_clock.Now);
            await SaveRun(run, workspaceDetails, cancellationToken);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            run.MarkCancelled(_clock.Now);
            await SaveRun(run, workspaceDetails, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while syncing connection {ConnectionId}.", connectionId);
            run.MarkFailed(_clock.Now, ex.Message);
            await SaveRun(run, workspaceDetails, CancellationToken.None);
            return Result.Failure(ex.Message);
        }
    }

    private async Task<WorkspaceSyncDetail> RunWorkspace(IWorkItemSource source, WorkspaceSyncTarget target, string systemId, SyncType syncType, Guid syncId, CancellationToken cancellationToken)
    {
        var prepResult = await source.PrepareWorkspaceForItemSync(target, syncId, cancellationToken);
        if (prepResult.IsFailure)
        {
            _logger.LogError("PrepareWorkspaceForItemSync failed for workspace {WorkspaceId}: {Error}", target.InternalWorkspaceId, prepResult.Error);
            return WorkspaceSyncDetail.FromFailure(target, prepResult.Error);
        }

        var iterationsResult = await source.SyncIterations(target, systemId, syncId, cancellationToken);
        if (iterationsResult.IsFailure)
        {
            _logger.LogError("SyncIterations failed for workspace {WorkspaceId}: {Error}", target.InternalWorkspaceId, iterationsResult.Error);
            return WorkspaceSyncDetail.FromFailure(target, iterationsResult.Error);
        }

        var itemsResult = await source.SyncWorkItems(target, syncType, syncId, cancellationToken);
        if (itemsResult.IsFailure)
        {
            _logger.LogError("SyncWorkItems failed for workspace {WorkspaceId}: {Error}", target.InternalWorkspaceId, itemsResult.Error);
            return WorkspaceSyncDetail.FromFailure(target, itemsResult.Error);
        }

        return WorkspaceSyncDetail.FromSuccess(target, itemsResult.Value);
    }

    private Task<Result<SyncableConnectionDescriptor>> BuildDescriptor(Guid connectionId, Connector connector, CancellationToken cancellationToken)
    {
        if (!_descriptorBuilders.TryGetValue(connector, out var builder))
            return Task.FromResult(Result.Failure<SyncableConnectionDescriptor>(
                $"No ISyncableConnectionDescriptorBuilder registered for connector '{connector}'."));

        return builder.Build(connectionId, cancellationToken);
    }

    private async Task SaveRun(SyncRun run, List<WorkspaceSyncDetail> details, CancellationToken cancellationToken)
    {
        if (details.Count > 0)
        {
            try
            {
                run.SetDetails(JsonSerializer.Serialize(details));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to serialize per-workspace details for SyncRun {SyncRunId}.", run.Id);
            }
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

}
