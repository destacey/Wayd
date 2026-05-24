using System.Text.Json;
using Ardalis.GuardClauses;
using MediatR;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Queries.AzureDevOps;
using Wayd.AppIntegration.Application.Interfaces;
using Wayd.AppIntegration.Application.Logging;
using Wayd.Common.Application.Enums;
using Wayd.Common.Application.Requests.Planning.Iterations;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.Requests.WorkManagement.Dtos;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;

namespace Wayd.AppIntegration.Application.Connections.Managers;

/// <summary>
/// Adapter that exposes the existing Azure DevOps integration as an <see cref="IWorkItemSource"/>.
/// The runner sees only the connector-neutral surface; AzDO's per-process iteration, team-settings,
/// and "since" lookup all live inside this class.
/// </summary>
public sealed class AzureDevOpsWorkItemSource(
    ILogger<AzureDevOpsWorkItemSource> logger,
    IAzureDevOpsService azureDevOpsService,
    ISender sender,
    IAzureDevOpsInitManager initManager) : IWorkItemSource
{
    public const string TeamSettingsFilterKey = "azdo.teamSettings";

    private static readonly DateTime _minSyncDate = new(1900, 01, 01);

    private readonly ILogger<AzureDevOpsWorkItemSource> _logger = logger;
    private readonly IAzureDevOpsService _azureDevOpsService = azureDevOpsService;
    private readonly ISender _sender = sender;
    private readonly IAzureDevOpsInitManager _initManager = initManager;

    private SyncableConnectionDescriptor? _descriptor;
    private AzureDevOpsBoardsConnectionConfiguration? _cfg;
    private AzureDevOpsBoardsTeamConfiguration? _teamCfg;

    // Captured during GetSyncPlan() so subsequent per-workspace calls can look up work-process
    // integration state without re-querying. Cleared on Bind().
    private List<AzureDevOpsWorkProcessDto>? _planWorkProcesses;
    private readonly HashSet<Guid> _workProcessesSynced = [];

    public Connector Connector => Connector.AzureDevOps;

    public Result Bind(SyncableConnectionDescriptor descriptor)
    {
        if (descriptor.Connector != Connector.AzureDevOps)
            return Result.Failure($"AzureDevOpsWorkItemSource cannot bind to connector '{descriptor.Connector}'.");

        if (descriptor.Configuration is not AzureDevOpsBoardsConnectionConfiguration cfg)
            return Result.Failure("Connection configuration is not an AzureDevOpsBoardsConnectionConfiguration.");

        _descriptor = descriptor;
        _cfg = cfg;
        _teamCfg = descriptor.TeamConfiguration as AzureDevOpsBoardsTeamConfiguration;
        _planWorkProcesses = null;
        _workProcessesSynced.Clear();
        return Result.Success();
    }

    public Task<Result> TestConnection(CancellationToken cancellationToken)
    {
        var ctx = RequireBound();
        return _azureDevOpsService.TestConnection(ctx.OrganizationUrl, ctx.PersonalAccessToken);
    }

    public Task<Result<string>> GetSystemId(CancellationToken cancellationToken)
    {
        var ctx = RequireBound();
        return _azureDevOpsService.GetSystemId(ctx.OrganizationUrl, ctx.PersonalAccessToken, cancellationToken);
    }

    public Task<Result> RefreshOrganizationConfiguration(Guid syncId, CancellationToken cancellationToken)
    {
        var connectionId = RequireDescriptor().ConnectionId;
        return _initManager.SyncOrganizationConfiguration(connectionId, cancellationToken, syncId);
    }

    public async Task<Result<IReadOnlyList<WorkspaceSyncTarget>>> GetSyncPlan(CancellationToken cancellationToken)
    {
        var connectionId = RequireDescriptor().ConnectionId;

        // Load fresh state so workspace/process integration flags reflect the most recent
        // RefreshOrganizationConfiguration() result.
        var connection = await _sender.Send(new GetAzureDevOpsConnectionQuery(connectionId), cancellationToken);
        if (connection is null)
            return Result.Failure<IReadOnlyList<WorkspaceSyncTarget>>($"Unable to load Azure DevOps connection {connectionId}.");

        // Capture the freshly-loaded work-process list (DTO shape) so subsequent per-workspace
        // calls can look up integration state without re-querying.
        _planWorkProcesses = connection.Configuration.WorkProcesses;

        var activeWorkProcessIds = connection.Configuration.WorkProcesses
            .Where(wp => wp.IntegrationState is not null && wp.IntegrationState.IsActive)
            .Select(wp => wp.ExternalId)
            .ToHashSet();

        if (activeWorkProcessIds.Count == 0)
            return Result.Success<IReadOnlyList<WorkspaceSyncTarget>>([]);

        var workspaceTeamsLookup = connection.TeamConfiguration?.WorkspaceTeams is not null
            ? connection.TeamConfiguration.WorkspaceTeams.GroupBy(t => t.WorkspaceId).ToDictionary(g => g.Key, g => g.ToArray())
            : [];

        var targets = new List<WorkspaceSyncTarget>();
        foreach (var workspace in connection.Configuration.Workspaces)
        {
            if (workspace.IntegrationState is null || !workspace.IntegrationState.IsActive)
                continue;
            if (!activeWorkProcessIds.Contains(workspace.WorkProcessId))
                continue;

            var workspaceTeams = workspaceTeamsLookup.TryGetValue(workspace.ExternalId, out var wt) ? wt : [];
            BuildTeamSettingsAndMappings(workspaceTeams, out var teamSettings, out var teamMappings);

            var filterPayload = new Dictionary<string, string>
            {
                [TeamSettingsFilterKey] = SerializeGuidNullableGuidMap(teamSettings),
                [TeamMappingsFilterKey] = SerializeGuidNullableGuidMap(teamMappings),
                [WorkProcessExternalIdFilterKey] = workspace.WorkProcessId.ToString(),
            };

            targets.Add(new WorkspaceSyncTarget(
                ExternalWorkspaceId: workspace.ExternalId,
                InternalWorkspaceId: workspace.IntegrationState.InternalId,
                WorkspaceName: workspace.Name,
                WorkspaceKey: workspace.ExternalId.ToString(),
                Filters: new WorkItemSyncFilters(filterPayload)));
        }

        return Result.Success<IReadOnlyList<WorkspaceSyncTarget>>(targets);
    }

    public async Task<Result> PrepareWorkspaceForItemSync(WorkspaceSyncTarget target, Guid syncId, CancellationToken cancellationToken)
    {
        var ctx = RequireBound();

        // Look up the work-process internal id from the fresh configuration.
        if (!TryGetWorkProcessForTarget(target, out var workProcessExternalId, out var workProcessInternalId, out var error))
            return Result.Failure(error!);

        // Sync the work process once per source instance (per connection per sync run).
        if (_workProcessesSynced.Add(workProcessExternalId))
        {
            var processResult = await SyncWorkProcess(ctx, syncId, workProcessExternalId, workProcessInternalId, cancellationToken);
            if (processResult.IsFailure)
                return processResult;
        }

        // Workspace metadata refresh.
        var workspaceResult = await ExternalCallMeasure.MeasureAsync(_logger, "Azdo_Sync_GetWorkspace",
            () => _azureDevOpsService.GetWorkspace(ctx.OrganizationUrl, ctx.PersonalAccessToken, target.ExternalWorkspaceId, cancellationToken), syncId);
        if (workspaceResult.IsFailure)
            return workspaceResult.ConvertFailure();

        var updateResult = await _sender.Send(new UpdateExternalWorkspaceCommand(workspaceResult.Value), cancellationToken);
        return updateResult;
    }

    public async Task<Result> SyncIterations(WorkspaceSyncTarget target, string systemId, Guid syncId, CancellationToken cancellationToken)
    {
        var ctx = RequireBound();
        var (teamSettings, teamMappings) = DeserializeTeamMaps(target);

        var iterationsResult = await ExternalCallMeasure.MeasureAsync(_logger, "Azdo_Sync_GetIterations",
            () => _azureDevOpsService.GetIterations(ctx.OrganizationUrl, ctx.PersonalAccessToken, target.WorkspaceName, teamSettings, cancellationToken), syncId);
        if (iterationsResult.IsFailure)
            return iterationsResult.ConvertFailure();

        return await _sender.Send(new SyncAzureDevOpsIterationsCommand(systemId, iterationsResult.Value, teamMappings), cancellationToken);
    }

    public async Task<Result<WorkspaceItemsSyncResult>> SyncWorkItems(WorkspaceSyncTarget target, SyncType syncType, Guid syncId, CancellationToken cancellationToken)
    {
        var ctx = RequireBound();
        var (teamSettings, teamMappings) = DeserializeTeamMaps(target);

        var lastChangedDate = syncType switch
        {
            SyncType.Full => _minSyncDate,
            SyncType.Differential => await GetWorkspaceMostRecentChangeDate(target.InternalWorkspaceId, cancellationToken),
            _ => _minSyncDate
        };

        var workTypesResult = await _sender.Send(new GetWorkspaceWorkTypesQuery(target.InternalWorkspaceId), cancellationToken);
        if (workTypesResult.IsFailure)
            return workTypesResult.ConvertFailure<WorkspaceItemsSyncResult>();

        var workTypeNames = workTypesResult.Value.Select(t => t.Name).ToArray();

        int workItems = 0, parents = 0, deps = 0, deleted = 0;
        var hadPartialFailure = false;
        var failureMessages = new List<string>();

        // Work items
        var workItemsResult = await ExternalCallMeasure.MeasureAsync(_logger, "Azdo_Sync_GetWorkItems",
            () => _azureDevOpsService.GetWorkItems(ctx.OrganizationUrl, ctx.PersonalAccessToken, target.WorkspaceName, lastChangedDate, workTypeNames, teamSettings, cancellationToken), syncId);
        if (workItemsResult.IsFailure)
        {
            _logger.LogError("Failed to retrieve work items for workspace {WorkspaceId}. Error: {Error}", target.InternalWorkspaceId, workItemsResult.Error);
            hadPartialFailure = true;
            failureMessages.Add($"work-items: {workItemsResult.Error}");
        }
        else if (workItemsResult.Value.Count > 0)
        {
            var iterationMappings = await _sender.Send(new GetIterationMappingsQuery(Connector.AzureDevOps, ctx.SystemId), cancellationToken);
            var syncResult = await _sender.Send(new SyncExternalWorkItemsCommand(target.InternalWorkspaceId, workItemsResult.Value, teamMappings, iterationMappings), cancellationToken);
            if (syncResult.IsFailure)
            {
                hadPartialFailure = true;
                failureMessages.Add($"work-items-sync: {syncResult.Error}");
            }
            else
            {
                workItems = workItemsResult.Value.Count;
            }
        }

        // Parent link changes (Differential only)
        if (syncType == SyncType.Differential)
        {
            var parentsResult = await ExternalCallMeasure.MeasureAsync(_logger, "Azdo_Sync_GetParentLinkChanges",
                () => _azureDevOpsService.GetParentLinkChanges(ctx.OrganizationUrl, ctx.PersonalAccessToken, target.WorkspaceName, lastChangedDate, workTypeNames, cancellationToken), syncId);
            if (parentsResult.IsFailure)
            {
                _logger.LogError("Failed to retrieve parent link changes for workspace {WorkspaceId}. Error: {Error}", target.InternalWorkspaceId, parentsResult.Error);
                hadPartialFailure = true;
                failureMessages.Add($"parents: {parentsResult.Error}");
            }
            else if (parentsResult.Value.Count > 0)
            {
                var syncResult = await _sender.Send(new SyncExternalWorkItemParentChangesCommand(target.InternalWorkspaceId, parentsResult.Value), cancellationToken);
                if (syncResult.IsFailure)
                {
                    hadPartialFailure = true;
                    failureMessages.Add($"parents-sync: {syncResult.Error}");
                }
                else
                {
                    parents = parentsResult.Value.Count;
                }
            }
        }

        // Dependency link changes
        var depsResult = await ExternalCallMeasure.MeasureAsync(_logger, "Azdo_Sync_GetDependencyLinkChanges",
            () => _azureDevOpsService.GetDependencyLinkChanges(ctx.OrganizationUrl, ctx.PersonalAccessToken, target.WorkspaceName, lastChangedDate, workTypeNames, cancellationToken), syncId);
        if (depsResult.IsFailure)
        {
            _logger.LogError("Failed to retrieve dependency link changes for workspace {WorkspaceId}. Error: {Error}", target.InternalWorkspaceId, depsResult.Error);
            hadPartialFailure = true;
            failureMessages.Add($"deps: {depsResult.Error}");
        }
        else if (depsResult.Value.Count > 0)
        {
            var syncResult = await _sender.Send(new SyncExternalWorkItemDependencyChangesCommand(target.InternalWorkspaceId, depsResult.Value), cancellationToken);
            if (syncResult.IsFailure)
            {
                hadPartialFailure = true;
                failureMessages.Add($"deps-sync: {syncResult.Error}");
            }
            else
            {
                deps = depsResult.Value.Count;
            }
        }

        // Deleted work items
        var deletedResult = await ExternalCallMeasure.MeasureAsync(_logger, "Azdo_Sync_GetDeletedWorkItemIds",
            () => _azureDevOpsService.GetDeletedWorkItemIds(ctx.OrganizationUrl, ctx.PersonalAccessToken, target.WorkspaceName, lastChangedDate, cancellationToken), syncId);
        if (deletedResult.IsFailure)
        {
            _logger.LogError("Failed to retrieve deleted work item ids for workspace {WorkspaceId}. Error: {Error}", target.InternalWorkspaceId, deletedResult.Error);
            hadPartialFailure = true;
            failureMessages.Add($"deleted: {deletedResult.Error}");
        }
        else if (deletedResult.Value.Length > 0)
        {
            var syncResult = await _sender.Send(new DeleteExternalWorkItemsCommand(target.InternalWorkspaceId, deletedResult.Value), cancellationToken);
            if (syncResult.IsFailure)
            {
                hadPartialFailure = true;
                failureMessages.Add($"deleted-sync: {syncResult.Error}");
            }
            else
            {
                deleted = deletedResult.Value.Length;
            }
        }

        return Result.Success(new WorkspaceItemsSyncResult(
            WorkItemsProcessed: workItems,
            ParentLinkChangesProcessed: parents,
            DependencyLinkChangesProcessed: deps,
            DeletedWorkItemsProcessed: deleted,
            HadPartialFailure: hadPartialFailure,
            PartialFailureMessage: hadPartialFailure ? string.Join("; ", failureMessages) : null));
    }

    // ----- helpers -----

    private const string TeamMappingsFilterKey = "azdo.teamMappings";
    private const string WorkProcessExternalIdFilterKey = "azdo.workProcessExternalId";

    private SyncContext RequireBound()
    {
        if (_descriptor is null || _cfg is null)
            throw new InvalidOperationException("AzureDevOpsWorkItemSource has not been bound. Call Bind(descriptor) before invoking sync methods.");
        return new SyncContext(_cfg.OrganizationUrl, _cfg.PersonalAccessToken, _descriptor.SystemId ?? string.Empty);
    }

    private SyncableConnectionDescriptor RequireDescriptor()
    {
        if (_descriptor is null)
            throw new InvalidOperationException("AzureDevOpsWorkItemSource has not been bound.");
        return _descriptor;
    }

    private async Task<Result> SyncWorkProcess(SyncContext ctx, Guid syncId, Guid workProcessExternalId, Guid workProcessInternalId, CancellationToken cancellationToken)
    {
        Guard.Against.Default(workProcessExternalId, nameof(workProcessExternalId));
        Guard.Against.Default(workProcessInternalId, nameof(workProcessInternalId));

        var processResult = await ExternalCallMeasure.MeasureAsync(_logger, "Azdo_Sync_GetWorkProcess",
            () => _azureDevOpsService.GetWorkProcess(ctx.OrganizationUrl, ctx.PersonalAccessToken, workProcessExternalId, cancellationToken), syncId);
        if (processResult.IsFailure)
            return processResult.ConvertFailure();

        var workTypes = processResult.Value.WorkTypes.OfType<Wayd.Common.Application.Interfaces.ExternalWork.IExternalWorkType>().ToList();
        if (workTypes.Count != 0)
        {
            var levels = await _sender.Send(new GetWorkTypeLevelsQuery(), cancellationToken);
            if (levels is null)
                return Result.Failure("Unable to get work type levels.");

            int defaultLevelId = -1;
            foreach (var l in levels)
            {
                if (l.Tier.Id == (int)Wayd.Common.Domain.Enums.Work.WorkTypeTier.Other)
                {
                    defaultLevelId = l.Id;
                    break;
                }
            }

            if (defaultLevelId == -1)
                return Result.Failure("Unable to get work type levels.");

            var syncWorkTypesResult = await _sender.Send(new SyncExternalWorkTypesCommand(workTypes, defaultLevelId), cancellationToken);
            if (syncWorkTypesResult.IsFailure)
                return syncWorkTypesResult;
        }

        if (processResult.Value.WorkStatuses.Count != 0)
        {
            var syncWorkStatusesResult = await _sender.Send(new SyncExternalWorkStatusesCommand(processResult.Value.WorkStatuses), cancellationToken);
            if (syncWorkStatusesResult.IsFailure)
                return syncWorkStatusesResult;
        }

        var workProcessSchemes = await _sender.Send(new GetWorkProcessSchemesQuery(workProcessInternalId), cancellationToken);
        var workProcessSchemesByWorkTypeName = workProcessSchemes.ToDictionary(s => s.WorkType.Name);

        var workflowMappings = new List<CreateWorkProcessSchemeDto>(processResult.Value.WorkTypes.Count);
        foreach (var workType in processResult.Value.WorkTypes)
        {
            workProcessSchemesByWorkTypeName.TryGetValue(workType.Name, out var scheme);
            if (scheme is null || scheme.Workflow is null)
            {
                var createWorkflowResult = await _sender.Send(new CreateExternalWorkflowCommand(
                    $"{processResult.Value.Name} - {workType.Name}",
                    "Auto-generated workflow for Azure DevOps work process.",
                    workType), cancellationToken);
                if (createWorkflowResult.IsFailure)
                    return createWorkflowResult.ConvertFailure();

                workflowMappings.Add(CreateWorkProcessSchemeDto.Create(workType.Name, workType.IsActive, createWorkflowResult.Value));
            }
            else
            {
                var syncWorkflowResult = await _sender.Send(new UpdateExternalWorkflowCommand(scheme.Workflow.Id, scheme.Workflow.Name, scheme.Workflow.Description, workType), cancellationToken);
                if (syncWorkflowResult.IsFailure)
                    return syncWorkflowResult;

                workflowMappings.Add(CreateWorkProcessSchemeDto.Create(workType.Name, workType.IsActive, scheme.Workflow.Id));
            }
        }

        var updateWorkProcessResult = await _sender.Send(new UpdateExternalWorkProcessCommand(processResult.Value, processResult.Value.WorkTypes, workflowMappings), cancellationToken);
        return updateWorkProcessResult.IsSuccess ? Result.Success() : updateWorkProcessResult;
    }

    private bool TryGetWorkProcessForTarget(WorkspaceSyncTarget target, out Guid workProcessExternalId, out Guid workProcessInternalId, out string? error)
    {
        workProcessExternalId = default;
        workProcessInternalId = default;
        error = null;

        if (_planWorkProcesses is null)
        {
            error = "GetSyncPlan() must be invoked before PrepareWorkspaceForItemSync.";
            return false;
        }

        if (!target.Filters.TryGet(WorkProcessExternalIdFilterKey, out var raw) || !Guid.TryParse(raw, out var wpId))
        {
            error = $"WorkspaceSyncTarget for {target.WorkspaceName} is missing AzDO work-process metadata.";
            return false;
        }

        var workProcess = _planWorkProcesses.FirstOrDefault(wp => wp.ExternalId == wpId);
        if (workProcess?.IntegrationState is null)
        {
            error = $"Work process {wpId} is not integrated for this connection.";
            return false;
        }

        workProcessExternalId = workProcess.ExternalId;
        workProcessInternalId = workProcess.IntegrationState.InternalId;
        return true;
    }

    private async Task<DateTime> GetWorkspaceMostRecentChangeDate(Guid workspaceId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetWorkspaceMostRecentChangeDateQuery(workspaceId), cancellationToken);
        return result.IsSuccess && result.Value != null
            ? ((Instant)result.Value).ToDateTimeUtc()
            : _minSyncDate;
    }

    private static (Dictionary<Guid, Guid?> teamSettings, Dictionary<Guid, Guid?> teamMappings) DeserializeTeamMaps(WorkspaceSyncTarget target)
    {
        target.Filters.TryGet(TeamSettingsFilterKey, out var settingsJson);
        target.Filters.TryGet(TeamMappingsFilterKey, out var mappingsJson);
        return (
            DeserializeGuidNullableGuidMap(settingsJson),
            DeserializeGuidNullableGuidMap(mappingsJson));
    }

    private static string SerializeGuidNullableGuidMap(Dictionary<Guid, Guid?> map)
    {
        if (map.Count == 0)
            return "{}";
        var stringified = map.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value?.ToString());
        return JsonSerializer.Serialize(stringified);
    }

    private static Dictionary<Guid, Guid?> DeserializeGuidNullableGuidMap(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return [];
        var stringified = JsonSerializer.Deserialize<Dictionary<string, string?>>(json);
        if (stringified is null)
            return [];
        var result = new Dictionary<Guid, Guid?>(stringified.Count);
        foreach (var kvp in stringified)
        {
            if (!Guid.TryParse(kvp.Key, out var key)) continue;
            Guid? value = Guid.TryParse(kvp.Value, out var v) ? v : null;
            result[key] = value;
        }
        return result;
    }

    private static void BuildTeamSettingsAndMappings(AzureDevOpsWorkspaceTeamDto[] workspaceTeams, out Dictionary<Guid, Guid?> teamSettings, out Dictionary<Guid, Guid?> teamMappings)
    {
        if (workspaceTeams is null || workspaceTeams.Length == 0)
        {
            teamSettings = [];
            teamMappings = [];
            return;
        }

        teamSettings = new(workspaceTeams.Length);
        teamMappings = new(workspaceTeams.Length);
        foreach (var team in workspaceTeams)
        {
            if (team.InternalTeamId is null)
                continue;
            teamSettings[team.TeamId] = team.BoardId;
            teamMappings[team.TeamId] = team.InternalTeamId;
        }
    }

    private sealed record SyncContext(string OrganizationUrl, string PersonalAccessToken, string SystemId);
}
