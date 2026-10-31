using System.Text.Json;
using Ardalis.GuardClauses;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Queries.AzureDevOps;
using Wayd.AppIntegration.Application.Interfaces;
using Wayd.AppIntegration.Application.Logging;
using Wayd.Common.Application.Enums;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Models;
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
    IDispatcher dispatcher,
    IAzureDevOpsInitManager initManager,
    AzureDevOpsWorkProcessSyncTracker workProcessSyncTracker) : IWorkItemSource
{
    public const string TeamSettingsFilterKey = "azdo.teamSettings";

    private static readonly DateTime _minSyncDate = new(1900, 01, 01);

    private readonly ILogger<AzureDevOpsWorkItemSource> _logger = logger;
    private readonly IAzureDevOpsService _azureDevOpsService = azureDevOpsService;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IAzureDevOpsInitManager _initManager = initManager;
    private readonly AzureDevOpsWorkProcessSyncTracker _workProcessSyncTracker = workProcessSyncTracker;

    private SyncableConnectionDescriptor? _descriptor;
    private AzureDevOpsBoardsConnectionConfiguration? _cfg;
    private AzureDevOpsBoardsTeamConfiguration? _teamCfg;

    // Captured during GetSyncPlan() so subsequent per-workspace calls can look up work-process
    // integration state without re-querying. Cleared on Bind().
    private List<AzureDevOpsWorkProcessDto>? _planWorkProcesses;
    // Every project set up in Wayd, paused or not, captured in GetSyncPlan(); cleared on Bind().
    private List<SetUpProject>? _setUpProjects;
    // Work process external id → Wayd id, once resolved in this bind; cleared on Bind().
    private readonly Dictionary<Guid, Guid> _resolvedWorkProcesses = [];
    private readonly Dictionary<Guid, string[]> _workTypeNames = [];

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
        _setUpProjects = null;
        _resolvedWorkProcesses.Clear();
        _workTypeNames.Clear();
        return Result.Success();
    }

    public Task<Result> TestConnection(CancellationToken cancellationToken)
    {
        var ctx = RequireBound();
        return _azureDevOpsService.TestConnection(ctx.Connection);
    }

    public Task<Result<string>> GetSystemId(CancellationToken cancellationToken)
    {
        var ctx = RequireBound();
        return _azureDevOpsService.GetSystemId(ctx.Connection, cancellationToken);
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
        var connection = await _dispatcher.Send(new GetAzureDevOpsConnectionQuery(connectionId), cancellationToken);
        if (connection is null)
            return Result.Failure<IReadOnlyList<WorkspaceSyncTarget>>($"Unable to load Azure DevOps connection {connectionId}.");

        // Capture the freshly-loaded work-process list (DTO shape) so subsequent per-workspace
        // calls can look up integration state without re-querying.
        _planWorkProcesses = connection.Configuration.WorkProcesses;
        _setUpProjects = [.. connection.Configuration.Workspaces
            .Where(w => w.IntegrationState is not null)
            .Select(w => new SetUpProject(w.Name, w.IntegrationState!.InternalId))];

        var workspaceTeamsLookup = connection.TeamConfiguration?.WorkspaceTeams is not null
            ? connection.TeamConfiguration.WorkspaceTeams.GroupBy(t => t.WorkspaceId).ToDictionary(g => g.Key, g => g.ToArray())
            : [];

        var targets = new List<WorkspaceSyncTarget>();
        foreach (var workspace in connection.Configuration.Workspaces)
        {
            if (workspace.IntegrationState is null || !workspace.IntegrationState.IsActive)
                continue;

            // A deactivated Wayd work process means its workspaces were deactivated first, which
            // does not reach the workspace's integration state, so those projects are skipped here.
            // A process Wayd does not have yet is still planned: preparing the project sets it up.
            var workProcess = connection.Configuration.WorkProcesses.FirstOrDefault(wp => wp.ExternalId == workspace.WorkProcessId);
            if (workProcess?.IntegrationState is { IsActive: false })
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

        var workProcessResult = await ResolveWorkProcess(target, cancellationToken);
        if (workProcessResult.IsFailure)
            return workProcessResult;
        var (workProcessExternalId, workProcessInternalId, justSetUp) = workProcessResult.Value;

        // Sync each work process once per run, whichever connection reaches it first. One set up
        // just now was read from Azure DevOps moments ago, so it is marked synced without a repeat.
        var firstInRun = _workProcessSyncTracker.TryMarkSynced(syncId, workProcessExternalId);
        if (firstInRun && !justSetUp)
        {
            var processResult = await SyncWorkProcess(ctx, syncId, workProcessExternalId, workProcessInternalId, cancellationToken);
            if (processResult.IsFailure)
                return processResult;
        }

        // The organization refresh moves a workspace to its project's new process only when that
        // process already exists in Wayd; one set up just now is assigned here.
        var assignResult = await _dispatcher.Send(new ChangeExternalWorkspaceWorkProcessCommand(target.InternalWorkspaceId, workProcessExternalId), cancellationToken);
        if (assignResult.IsFailure)
            return assignResult;

        // Workspace metadata refresh.
        var workspaceResult = await ExternalCallMeasure.MeasureAsync(_logger, "Azdo_Sync_GetWorkspace",
            () => _azureDevOpsService.GetWorkspace(ctx.Connection, target.ExternalWorkspaceId, cancellationToken), syncId);
        if (workspaceResult.IsFailure)
            return workspaceResult.ConvertFailure();

        var updateResult = await _dispatcher.Send(new UpdateExternalWorkspaceCommand(workspaceResult.Value), cancellationToken);
        return updateResult;
    }

    public async Task<Result> SyncIterations(WorkspaceSyncTarget target, string systemId, Guid syncId, CancellationToken cancellationToken)
    {
        var ctx = RequireBound();
        var (teamSettings, teamMappings) = DeserializeTeamMaps(target);

        var iterationsResult = await ExternalCallMeasure.MeasureAsync(_logger, "Azdo_Sync_GetIterations",
            () => _azureDevOpsService.GetIterations(ctx.Connection, target.WorkspaceName, teamSettings, cancellationToken), syncId);
        if (iterationsResult.IsFailure)
            return iterationsResult.ConvertFailure();

        return await _dispatcher.Send(new SyncAzureDevOpsIterationsCommand(systemId, iterationsResult.Value, teamMappings), cancellationToken);
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

        var workTypesResult = await _dispatcher.Send(new GetWorkspaceWorkTypesQuery(target.InternalWorkspaceId), cancellationToken);
        if (workTypesResult.IsFailure)
            return workTypesResult.ConvertFailure<WorkspaceItemsSyncResult>();

        var workTypeNames = workTypesResult.Value.Select(t => t.Name).ToArray();

        int workItems = 0, parents = 0, deps = 0, deleted = 0;
        var hadPartialFailure = false;
        var failureMessages = new List<string>();

        // Work items
        var workItemsResult = await ExternalCallMeasure.MeasureAsync(_logger, "Azdo_Sync_GetWorkItems",
            () => _azureDevOpsService.GetWorkItems(ctx.Connection, target.WorkspaceName, lastChangedDate, workTypeNames, teamSettings, cancellationToken), syncId);
        if (workItemsResult.IsFailure)
        {
            _logger.LogError("Failed to retrieve work items for workspace {WorkspaceId}. Error: {Error}", target.InternalWorkspaceId, workItemsResult.Error);
            hadPartialFailure = true;
            failureMessages.Add($"work-items: {workItemsResult.Error}");
        }
        else if (workItemsResult.Value.Count > 0)
        {
            var iterationMappings = await _dispatcher.Send(new GetIterationMappingsQuery(Connector.AzureDevOps, ctx.SystemId), cancellationToken);
            var syncResult = await _dispatcher.Send(new SyncExternalWorkItemsCommand(RequireDescriptor().ConnectionId, Connector.AzureDevOps, target.InternalWorkspaceId, workItemsResult.Value, teamMappings, iterationMappings), cancellationToken);
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
                () => _azureDevOpsService.GetParentLinkChanges(ctx.Connection, target.WorkspaceName, lastChangedDate, workTypeNames, cancellationToken), syncId);
            if (parentsResult.IsFailure)
            {
                _logger.LogError("Failed to retrieve parent link changes for workspace {WorkspaceId}. Error: {Error}", target.InternalWorkspaceId, parentsResult.Error);
                hadPartialFailure = true;
                failureMessages.Add($"parents: {parentsResult.Error}");
            }
            else if (parentsResult.Value.Count > 0)
            {
                var syncResult = await _dispatcher.Send(new SyncExternalWorkItemParentChangesCommand(target.InternalWorkspaceId, parentsResult.Value), cancellationToken);
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
            () => _azureDevOpsService.GetDependencyLinkChanges(ctx.Connection, target.WorkspaceName, lastChangedDate, workTypeNames, cancellationToken), syncId);
        if (depsResult.IsFailure)
        {
            _logger.LogError("Failed to retrieve dependency link changes for workspace {WorkspaceId}. Error: {Error}", target.InternalWorkspaceId, depsResult.Error);
            hadPartialFailure = true;
            failureMessages.Add($"deps: {depsResult.Error}");
        }
        else if (depsResult.Value.Count > 0)
        {
            var syncResult = await _dispatcher.Send(new SyncExternalWorkItemDependencyChangesCommand(target.InternalWorkspaceId, depsResult.Value), cancellationToken);
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
            () => _azureDevOpsService.GetDeletedWorkItemIds(ctx.Connection, target.WorkspaceName, lastChangedDate, workTypeNames, cancellationToken), syncId);
        if (deletedResult.IsFailure)
        {
            _logger.LogError("Failed to retrieve deleted work item ids for workspace {WorkspaceId}. Error: {Error}", target.InternalWorkspaceId, deletedResult.Error);
            hadPartialFailure = true;
            failureMessages.Add($"deleted: {deletedResult.Error}");
        }
        else if (deletedResult.Value.Length > 0)
        {
            var syncResult = await _dispatcher.Send(new DeleteExternalWorkItemsCommand(SetUpWorkspaceIds(target), deletedResult.Value), cancellationToken);
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

    /// <summary>
    /// Returns one page of the project's reporting revisions, of the workspace's synced work types.
    /// The watermark is Azure DevOps' continuation token.
    /// </summary>
    /// <remarks>
    /// The type filter applies to each revision, so an item that changed into a synced type has
    /// revisions this stream skips; the runner reads them from the item through
    /// <see cref="GetAllRevisions"/>.
    /// </remarks>
    public async Task<Result<WorkItemHistoryBatch>> GetWorkItemHistory(WorkspaceSyncTarget target, string? watermark, CancellationToken cancellationToken)
    {
        var ctx = RequireBound();

        var workTypeNamesResult = await GetWorkTypeNames(target.InternalWorkspaceId, cancellationToken);
        if (workTypeNamesResult.IsFailure)
            return workTypeNamesResult.ConvertFailure<WorkItemHistoryBatch>();

        var revisionsResult = await _azureDevOpsService.GetWorkItemRevisions(ctx.Connection, target.WorkspaceName, watermark, workTypeNamesResult.Value, cancellationToken);
        if (revisionsResult.IsFailure)
            return revisionsResult.ConvertFailure<WorkItemHistoryBatch>();

        var batch = revisionsResult.Value;
        return new WorkItemHistoryBatch(batch.Revisions, batch.ContinuationToken ?? watermark, batch.IsLastBatch);
    }

    /// <summary>
    /// Reads each item's revisions from the item itself, which Azure DevOps answers whichever
    /// projects the item was in and whatever its type was.
    /// </summary>
    public async Task<Result<IReadOnlyList<IExternalWorkItemRevision>>> GetAllRevisions(WorkspaceSyncTarget target, IReadOnlyCollection<int> workItemIds, CancellationToken cancellationToken)
    {
        var ctx = RequireBound();

        var result = await _azureDevOpsService.GetRevisionsOfWorkItems(ctx.Connection, workItemIds, cancellationToken);
        return result.IsSuccess
            ? result.Value
            : result.ConvertFailure<IReadOnlyList<IExternalWorkItemRevision>>();
    }

    // ----- helpers -----

    private const string TeamMappingsFilterKey = "azdo.teamMappings";
    private const string WorkProcessExternalIdFilterKey = "azdo.workProcessExternalId";

    private SyncContext RequireBound()
    {
        if (_descriptor is null || _cfg is null)
            throw new InvalidOperationException("AzureDevOpsWorkItemSource has not been bound. Call Bind(descriptor) before invoking sync methods.");
        return new SyncContext(new AzureDevOpsConnectionContext(_cfg.Organization, _cfg.PersonalAccessToken), _descriptor.SystemId ?? string.Empty);
    }

    /// <summary>
    /// The Wayd workspaces of every project this connection has set up, the target's included. A
    /// deleted or retyped item may still sit in the workspace of a project it left, so deletes
    /// reach all of them.
    /// </summary>
    private Guid[] SetUpWorkspaceIds(WorkspaceSyncTarget target) =>
        [.. (_setUpProjects ?? []).Select(p => p.InternalWorkspaceId).Append(target.InternalWorkspaceId).Distinct()];

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
            () => _azureDevOpsService.GetWorkProcess(ctx.Connection, workProcessExternalId, cancellationToken), syncId);
        if (processResult.IsFailure)
            return processResult.ConvertFailure();

        var workTypes = processResult.Value.WorkTypes.OfType<Wayd.Common.Application.Interfaces.ExternalWork.IExternalWorkType>().ToList();
        if (workTypes.Count != 0)
        {
            var levels = await _dispatcher.Send(new GetWorkTypeLevelsQuery(), cancellationToken);
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

            var syncWorkTypesResult = await _dispatcher.Send(new SyncExternalWorkTypesCommand(workTypes, defaultLevelId), cancellationToken);
            if (syncWorkTypesResult.IsFailure)
                return syncWorkTypesResult;
        }

        if (processResult.Value.WorkStatuses.Count != 0)
        {
            var syncWorkStatusesResult = await _dispatcher.Send(new SyncExternalWorkStatusesCommand(processResult.Value.WorkStatuses), cancellationToken);
            if (syncWorkStatusesResult.IsFailure)
                return syncWorkStatusesResult;
        }

        var workProcessSchemes = await _dispatcher.Send(new GetWorkProcessSchemesQuery(workProcessInternalId), cancellationToken);
        if (workProcessSchemes is null)
            return Result.Failure($"Work process {workProcessInternalId} not found.");

        var workProcessSchemesByWorkTypeName = workProcessSchemes.ToDictionary(s => s.WorkType.Name);

        var workflowMappings = new List<CreateWorkProcessSchemeDto>(processResult.Value.WorkTypes.Count);
        foreach (var workType in processResult.Value.WorkTypes)
        {
            workProcessSchemesByWorkTypeName.TryGetValue(workType.Name, out var scheme);
            if (scheme is null || scheme.Workflow is null)
            {
                var createWorkflowResult = await _dispatcher.Send(new CreateExternalWorkflowCommand(
                    $"{processResult.Value.Name} - {workType.Name}",
                    AzureDevOpsInitManager.AutoGeneratedWorkflowDescription,
                    workType), cancellationToken);
                if (createWorkflowResult.IsFailure)
                    return createWorkflowResult.ConvertFailure();

                workflowMappings.Add(CreateWorkProcessSchemeDto.Create(workType.Name, workType.IsActive, createWorkflowResult.Value));
            }
            else
            {
                var syncWorkflowResult = await _dispatcher.Send(new UpdateExternalWorkflowCommand(scheme.Workflow.Id, scheme.Workflow.Name, scheme.Workflow.Description, workType), cancellationToken);
                if (syncWorkflowResult.IsFailure)
                    return syncWorkflowResult;

                workflowMappings.Add(CreateWorkProcessSchemeDto.Create(workType.Name, workType.IsActive, scheme.Workflow.Id));
            }
        }

        var updateWorkProcessResult = await _dispatcher.Send(new UpdateExternalWorkProcessCommand(processResult.Value, processResult.Value.WorkTypes, workflowMappings), cancellationToken);
        return updateWorkProcessResult.IsSuccess ? Result.Success() : updateWorkProcessResult;
    }

    /// <summary>
    /// The target project's work process, external and Wayd ids. A process this connection has not
    /// set up yet, such as one a project moved to in Azure DevOps, is set up here.
    /// </summary>
    /// <returns>
    /// The process's external and Wayd ids, and whether it was set up by this call, in which case
    /// it was read from Azure DevOps moments ago.
    /// </returns>
    private async Task<Result<(Guid ExternalId, Guid InternalId, bool JustSetUp)>> ResolveWorkProcess(WorkspaceSyncTarget target, CancellationToken cancellationToken)
    {
        if (_planWorkProcesses is null)
            return Result.Failure<(Guid, Guid, bool)>("GetSyncPlan() must be invoked before PrepareWorkspaceForItemSync.");

        if (!target.Filters.TryGet(WorkProcessExternalIdFilterKey, out var raw) || !Guid.TryParse(raw, out var wpId))
            return Result.Failure<(Guid, Guid, bool)>($"WorkspaceSyncTarget for {target.WorkspaceName} is missing AzDO work-process metadata.");

        if (wpId == Guid.Empty)
            return Result.Failure<(Guid, Guid, bool)>($"Azure DevOps reports no work process for project {target.WorkspaceName}.");

        if (_resolvedWorkProcesses.TryGetValue(wpId, out var resolvedInternalId))
            return (wpId, resolvedInternalId, false);

        var workProcess = _planWorkProcesses.FirstOrDefault(wp => wp.ExternalId == wpId);
        if (workProcess?.IntegrationState is { IsActive: true })
        {
            _resolvedWorkProcesses[wpId] = workProcess.IntegrationState.InternalId;
            return (wpId, workProcess.IntegrationState.InternalId, false);
        }

        var ensureResult = await _initManager.EnsureWorkProcessIntegration(RequireDescriptor().ConnectionId, wpId, cancellationToken);
        if (ensureResult.IsFailure)
            return Result.Failure<(Guid, Guid, bool)>($"Unable to set up the work process for project {target.WorkspaceName}. {ensureResult.Error}");

        _resolvedWorkProcesses[wpId] = ensureResult.Value;
        return (wpId, ensureResult.Value, true);
    }

    /// <summary>
    /// The workspace's work type names, read once per bind: the history sync asks for them on
    /// every page, and a backfill reads thousands of pages.
    /// </summary>
    private async Task<Result<string[]>> GetWorkTypeNames(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (_workTypeNames.TryGetValue(workspaceId, out var cached))
            return cached;

        var workTypesResult = await _dispatcher.Send(new GetWorkspaceWorkTypesQuery(workspaceId), cancellationToken);
        if (workTypesResult.IsFailure)
            return workTypesResult.ConvertFailure<string[]>();

        var names = workTypesResult.Value.Select(t => t.Name).ToArray();
        _workTypeNames[workspaceId] = names;
        return names;
    }

    private async Task<DateTime> GetWorkspaceMostRecentChangeDate(Guid workspaceId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetWorkspaceMostRecentChangeDateQuery(workspaceId), cancellationToken);
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

    /// <summary>A project set up in Wayd: its Azure DevOps name and its Wayd workspace.</summary>
    private sealed record SetUpProject(string Name, Guid InternalWorkspaceId);

    private sealed record SyncContext(AzureDevOpsConnectionContext Connection, string SystemId);
}
