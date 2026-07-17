using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Domain.Interfaces;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.Application.WorkItems.Commands;

// TODO: add validation

public sealed class SyncExternalWorkItemsCommandHandler(IWorkDbContext workDbContext, ILogger<SyncExternalWorkItemsCommandHandler> logger) : ICommandHandler<SyncExternalWorkItemsCommand>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<SyncExternalWorkItemsCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncExternalWorkItemsCommand request, CancellationToken cancellationToken)
    {
        if (request.WorkItems.Count == 0)
        {
            return Result.Success();
        }

        var workspace = await _workDbContext.Workspaces
            .FirstOrDefaultAsync(w => w.Id == request.WorkspaceId && w.OwnershipInfo.Ownership == Ownership.Managed, cancellationToken);
        if (workspace is null)
        {
            _logger.LogWarning("Unable to sync external work items for workspace {WorkspaceId} because the workspace does not exist.", request.WorkspaceId);
            return Result.Failure($"Unable to sync external work items for workspace {request.WorkspaceId} because the workspace does not exist.");
        }

        var syncLog = new WorkItemSyncLog(request.WorkItems.Count);

        try
        {
            // TODO: Do we need to do anything here for work items changing workspaces?
            var workProcess = await _workDbContext.WorkProcesses
                .AsNoTracking()
                .Include(p => p.Schemes)
                    .ThenInclude(s => s.WorkType)
                        .ThenInclude(t => t!.Level)
                .Include(p => p.Schemes)
                    .ThenInclude(s => s.Workflow)
                        .ThenInclude(wf => wf!.Schemes)
                            .ThenInclude(wfs => wfs.WorkStatus)
                .AsSplitQuery()
                .FirstOrDefaultAsync(wp => wp.Id == workspace.WorkProcessId, cancellationToken);
            if (workProcess is null)
            {
                _logger.LogWarning("Unable to sync external work items for workspace {WorkspaceId} because the workspace does not have a work process.", workspace.Id);
                return Result.Failure($"Unable to sync external work items for workspace {workspace.Id} because the workspace does not have a work process.");
            }

            var workTypes = new HashSet<WorkType>();
            var workStatusMappings = new HashSet<WorkStatusMapping>();

            foreach (var scheme in workProcess.Schemes)
            {
                if (scheme.WorkType is null || scheme.Workflow is null || scheme.Workflow.Schemes.Count == 0 || !scheme.Workflow.Schemes.Any(s => s.WorkStatus != null))
                {
                    continue;
                }

                workTypes.Add(scheme.WorkType);
                foreach (var workflowScheme in scheme.Workflow.Schemes)
                {
                    if (workflowScheme.WorkStatus is not null)
                    {
                        workStatusMappings.Add(new WorkStatusMapping(scheme.WorkTypeId, workflowScheme.WorkStatus, workflowScheme.WorkStatusCategory));
                    }
                }
            }

            // Build dictionaries for fast lookups
            var workTypeByName = workTypes.ToDictionary(t => t.Name, t => t);
            var workStatusMap = workStatusMappings.ToDictionary(m => (m.WorkTypeId, m.WorkStatus.Name), m => m);

            // Build dictionary for employees referenced by the incoming payload to avoid loading all employees
            var referencedEmails = request.WorkItems
                .SelectMany(w => new[] { w.CreatedBy, w.LastModifiedBy, w.AssignedTo })
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // Batch the employee queries to avoid very large IN lists (SQL parameter limits)
            const int emailBatchSize = 1000;
            var employeesByEmail = new Dictionary<string, Guid>(referencedEmails.Length, StringComparer.OrdinalIgnoreCase);
            if (referencedEmails.Length > 0)
            {
                foreach (var batch in referencedEmails.Chunk(emailBatchSize))
                {
                    var rows = await _workDbContext.Employees
                        .Where(e => batch.Contains(e.Email))
                        .Select(e => new { e.Id, e.Email })
                        .ToListAsync(cancellationToken);

                    foreach (var r in rows)
                    {
                        employeesByEmail[r.Email.Value] = r.Id;
                    }
                }
            }

            // Handle workspace changes separately before main batch processing
            await ProcessWorkspaceChanges(workspace, request.WorkItems, workTypeByName, workStatusMap, cancellationToken);

            // Load parents after workspace changes to ensure we have the current state
            var referencedParentIds = request.WorkItems
                .Where(w => w.ParentId.HasValue)
                .Select(w => w.ParentId!.Value)
                .Distinct()
                .ToArray();

            var potentialParents = await LoadPortfolioParentsByExternalIds(
                workspace.OwnershipInfo.SystemId!,
                referencedParentIds,
                cancellationToken);

            var hasIterationMappings = request.IterationMappings != null && request.IterationMappings.Count > 0;

            int chunkSize = 2000;
            var chunks = request.WorkItems.OrderBy(w => w.LastModified).Chunk(chunkSize).ToList();

            var missingParents = new Dictionary<int, int>();
            int c = 1;
            foreach (var chunk in chunks)
            {
                // Query existing work items for this chunk directly instead of loading the workspace navigation collection
                var chunkExternalIds = chunk.Select(x => x.Id).ToArray();
                var existingWorkItems = await _workDbContext.WorkItems
                    .Include(wi => wi.ExtendedProps)
                    .Include(wi => wi.Type)
                        .ThenInclude(t => t.Level)
                    .Where(w => w.Workspace.OwnershipInfo.SystemId == workspace.OwnershipInfo.SystemId)
                    .Where(wi => wi.ExternalId != null && chunkExternalIds.Contains(wi.ExternalId.Value))
                    .ToListAsync(cancellationToken);

                var existingByExternalId = existingWorkItems
                    .Where(wi => wi.ExternalId.HasValue)
                    .ToDictionary(wi => wi.ExternalId!.Value, wi => wi);

                List<WorkItem> newWorkItems = new(chunkSize);

                foreach (var externalWorkItem in chunk)
                {
                    syncLog.ItemRequested(externalWorkItem.Id);

                    if (!workTypeByName.TryGetValue(externalWorkItem.WorkType, out var workType))
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                            _logger.LogDebug("Unknown work type {WorkType} for external work item {ExternalId} in workspace {WorkspaceId}.", externalWorkItem.WorkType, externalWorkItem.Id, workspace.Id);
                        syncLog.ItemError();
                        continue;
                    }

                    if (!workStatusMap.TryGetValue((workType.Id, externalWorkItem.WorkStatus), out var workFlowSchemeData))
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                            _logger.LogDebug("Unknown work status {WorkStatus} for external work item {ExternalId} in workspace {WorkspaceId}.", externalWorkItem.WorkStatus, externalWorkItem.Id, workspace.Id);
                        syncLog.ItemError();
                        continue;
                    }

                    IWorkItemParentInfo? parentWorkItemInfo = null;
                    if (externalWorkItem.ParentId.HasValue)
                    {
                        if (!potentialParents.TryGetValue(externalWorkItem.ParentId.Value, out parentWorkItemInfo))
                        {
                            missingParents.TryAdd(externalWorkItem.Id, externalWorkItem.ParentId.Value);
                        }
                        else if (workType.Level is null || (workType.Level.Tier == WorkTypeTier.Portfolio && workType.Level.Order <= parentWorkItemInfo.LevelOrder))
                        {
                            if (_logger.IsEnabled(LogLevel.Debug))
                                _logger.LogDebug("The parent must be a higher level than the work item {ExternalId} in workspace {WorkspaceId}.", externalWorkItem.Id, workspace.Id);
                            parentWorkItemInfo = null;
                        }
                    }

                    existingByExternalId.TryGetValue(externalWorkItem.Id, out var workItem);
                    try
                    {
                        Guid? teamId = null;
                        if (externalWorkItem.TeamId.HasValue)
                        {
                            request.TeamMappings.TryGetValue(externalWorkItem.TeamId.Value, out teamId);
                        }

                        Guid? iterationId = null;
                        if (hasIterationMappings && externalWorkItem.IterationId.HasValue)
                        {
                            if (request.IterationMappings!.TryGetValue(externalWorkItem.IterationId.Value.ToString(), out var mappedIterationId))
                            {
                                iterationId = mappedIterationId;
                            }
                            else
                            {
                                if (_logger.IsEnabled(LogLevel.Debug))
                                    _logger.LogDebug("Unknown iteration mapping for external iteration id {ExternalIterationId} for work item {ExternalId} in workspace {WorkspaceId}.", externalWorkItem.IterationId.Value, externalWorkItem.Id, workspace.Id);
                            }
                        }

                        if (workItem is null)
                        {
                            var employeeIds = ResolveEmployeeIds(externalWorkItem, employeesByEmail);

                            workItem = WorkItem.CreateExternal(
                                workspace,
                                externalWorkItem.Id,
                                externalWorkItem.Title,
                                workType,
                                workFlowSchemeData.WorkStatus.Id,
                                workFlowSchemeData.WorkStatusCategory,
                                parentWorkItemInfo,
                                teamId,
                                externalWorkItem.Created,
                                employeeIds.CreatedById,
                                externalWorkItem.LastModified,
                                employeeIds.LastModifiedById,
                                employeeIds.AssignedToId,
                                externalWorkItem.Priority,
                                externalWorkItem.StackRank,
                                externalWorkItem.StoryPoints,
                                iterationId,
                                externalWorkItem.ActivatedTimestamp,
                                externalWorkItem.DoneTimestamp,
                                externalWorkItem.ExternalTeamIdentifier,
                                [.. externalWorkItem.Tags.Select(t => new WorkItemTag(t))]
                            );
                            newWorkItems.Add(workItem);

                            syncLog.ItemCreated();
                        }
                        else
                        {
                            var employeeIds = ResolveEmployeeIds(externalWorkItem, employeesByEmail);

                            workItem.Update(
                                externalWorkItem.Title,
                                workType,
                                workFlowSchemeData.WorkStatus.Id,
                                workFlowSchemeData.WorkStatusCategory,
                                parentWorkItemInfo,
                                teamId,
                                externalWorkItem.LastModified,
                                employeeIds.LastModifiedById,
                                employeeIds.AssignedToId,
                                externalWorkItem.Priority,
                                externalWorkItem.StackRank,
                                externalWorkItem.StoryPoints,
                                iterationId,
                                externalWorkItem.ActivatedTimestamp,
                                externalWorkItem.DoneTimestamp,
                                CreateExtendedPropsIfNeeded(workItem.Id, externalWorkItem.ExternalTeamIdentifier),
                                [.. externalWorkItem.Tags.Select(t => new WorkItemTag(t))]
                            );

                            syncLog.ItemUpdated();
                        }
                    }
                    catch (Exception ex)
                    {
                        if (workItem is not null && workItem.Id != Guid.Empty)
                        {
                            // Reset the entity
                            await _workDbContext.Entry(workItem).ReloadAsync(cancellationToken);
                            workItem.ClearDomainEvents();
                        }

                        _logger.LogError(ex, "Exception thrown while syncing external work item {ExternalId} in workspace {WorkspaceId} ({WorkspaceName}).", externalWorkItem.Id, workspace.Id, workspace.Name);

                        syncLog.ItemError();
                        continue;
                    }
                }

                if (newWorkItems.Count > 0)
                {
                    await _workDbContext.WorkItems.AddRangeAsync(newWorkItems, cancellationToken);
                }

                await _workDbContext.SaveChangesAsync(cancellationToken);

                // Clear the change tracker to release tracked entities and prevent memory bloat
                // needs a null check for tests
                _workDbContext.ChangeTracker?.Clear();

                _logger.LogInformation("Synced {ChunkCount} of {TotalChunks} for workspace {WorkspaceId} ({WorkspaceName}).", c++, chunks.Count, workspace.Id, workspace.Name);
            }

            await MapMissingParents(workspace, missingParents, cancellationToken);
            await SyncParentProjectIds(workspace, workTypes, cancellationToken);
        }
        catch (Exception ex)
        {
            // Reset the entity
            await _workDbContext.Entry(workspace).ReloadAsync(cancellationToken);
            workspace.ClearDomainEvents();

            _logger.LogError(ex, "An error occurred while syncing workspace {WorkspaceId} work items.", workspace.Id);
            return Result.Failure($"An error occurred while syncing workspace {workspace.Id} work items.");
        }
        finally
        {
            _logger.LogInformation("Synced {Processed} external work items for workspace {WorkspaceId}. Requested: {Requested}, Created: {Created}, Updated: {Updated}, Errors: {Errors}, Last External Id: {LastExternalId}.", syncLog.Processed, workspace.Id, syncLog.Requested, syncLog.Created, syncLog.Updated, syncLog.Errors, syncLog.LastExternalId);
        }

        return Result.Success();
    }

    private async Task<Dictionary<int, IWorkItemParentInfo>> LoadPortfolioParentsByExternalIds(
        string systemId,
        int[] externalIds,
        CancellationToken cancellationToken)
    {
        const int batchSize = 1000; // SQL parameter limit optimization

        var parents = new Dictionary<int, IWorkItemParentInfo>(externalIds.Length);

        if (externalIds.Length == 0)
            return parents;

        foreach (var batch in externalIds.Chunk(batchSize))
        {
            var batchParents = await _workDbContext.WorkItems
                .Where(w => w.Workspace.OwnershipInfo.SystemId == systemId)
                .Where(w => w.Type.Level != null && w.Type.Level.Tier == WorkTypeTier.Portfolio)
                .Where(w => w.ExternalId != null && batch.Contains(w.ExternalId.Value))
                .ProjectToType<WorkItemParentInfo>()
                .Where(p => p.ExternalId.HasValue)
                .ToListAsync(cancellationToken);

            foreach (var parent in batchParents)
            {
                parents[parent.ExternalId!.Value] = parent;
            }
        }

        return parents;
    }

    private async Task ProcessWorkspaceChanges(
        Workspace targetWorkspace,
        List<IExternalWorkItem> externalWorkItems,
        Dictionary<string, WorkType> workTypeByName,
        Dictionary<(int WorkTypeId, string StatusName), WorkStatusMapping> workStatusMap,
        CancellationToken cancellationToken)
    {
        // Find work items that may have changed workspaces
        var externalIds = externalWorkItems.Select(w => w.Id).ToArray();

        var workItemsInDifferentWorkspace = await _workDbContext.WorkItems
            .Include(wi => wi.Type)
                .ThenInclude(t => t.Level)
            .Where(w => w.Workspace.OwnershipInfo.SystemId == targetWorkspace.OwnershipInfo.SystemId)
            .Where(w => w.WorkspaceId != targetWorkspace.Id)
            .Where(w => w.ExternalId != null && externalIds.Contains(w.ExternalId.Value))
            .ToListAsync(cancellationToken);

        if (workItemsInDifferentWorkspace.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} work items that need workspace change to {WorkspaceId}.",
            workItemsInDifferentWorkspace.Count, targetWorkspace.Id);

        // Build lookup for external work items by their Id (which is the external id)
        var externalWorkItemsById = externalWorkItems.ToDictionary(w => w.Id, w => w);

        foreach (var workItem in workItemsInDifferentWorkspace)
        {
            if (!workItem.ExternalId.HasValue || !externalWorkItemsById.TryGetValue(workItem.ExternalId.Value, out var externalWorkItem))
            {
                continue;
            }

            if (!workTypeByName.TryGetValue(externalWorkItem.WorkType, out var workType))
            {
                _logger.LogWarning("Unknown work type {WorkType} for workspace change of work item {ExternalId}.",
                    externalWorkItem.WorkType, externalWorkItem.Id);
                continue;
            }

            if (!workStatusMap.TryGetValue((workType.Id, externalWorkItem.WorkStatus), out var workFlowSchemeData))
            {
                _logger.LogWarning("Unknown work status {WorkStatus} for workspace change of work item {ExternalId}.",
                    externalWorkItem.WorkStatus, externalWorkItem.Id);
                continue;
            }

            try
            {
                var result = workItem.ChangeExternalWorkspace(targetWorkspace, workType, workFlowSchemeData.WorkStatus.Id, workFlowSchemeData.WorkStatusCategory);
                if (result.IsFailure)
                {
                    _logger.LogError("Failed to change workspace for external work item {WorkItemId}: {Error}",
                        workItem.Id, result.Error);
                    continue;
                }

                _logger.LogInformation("External work item {WorkItemId} (External ID: {ExternalId}) moved to workspace {WorkspaceId}.",
                    workItem.Id, workItem.ExternalId, targetWorkspace.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while changing workspace for work item {ExternalId}.", workItem.ExternalId);
            }
        }

        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Completed workspace changes for {Count} work items.", workItemsInDifferentWorkspace.Count);
    }

    private static EmployeeIds ResolveEmployeeIds(
        IExternalWorkItem externalWorkItem,
        Dictionary<string, Guid> employeesByEmail)
    {
        Guid? createdById = null;
        if (!string.IsNullOrWhiteSpace(externalWorkItem.CreatedBy)
            && employeesByEmail.TryGetValue(externalWorkItem.CreatedBy, out var tmpCreated))
            createdById = tmpCreated;

        Guid? lastModifiedById = null;
        if (!string.IsNullOrWhiteSpace(externalWorkItem.LastModifiedBy)
            && employeesByEmail.TryGetValue(externalWorkItem.LastModifiedBy, out var tmpLastModified))
            lastModifiedById = tmpLastModified;

        Guid? assignedToId = null;
        if (!string.IsNullOrWhiteSpace(externalWorkItem.AssignedTo)
            && employeesByEmail.TryGetValue(externalWorkItem.AssignedTo, out var tmpAssigned))
            assignedToId = tmpAssigned;

        return new EmployeeIds(createdById, lastModifiedById, assignedToId);
    }

    private static WorkItemExtended? CreateExtendedPropsIfNeeded(Guid workItemId, string? externalTeamIdentifier)
    {
        return WorkItemExtended.Create(workItemId, externalTeamIdentifier);
    }

    private async Task MapMissingParents(Workspace workspace, Dictionary<int, int> missingParents, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Mapping {WorkItemCount} missing parents for workspace {WorkspaceId}.", missingParents.Count, workspace.Id);

        if (missingParents.Count == 0)
        {
            return;
        }

        const int batchSize = 1000; // SQL parameter limit optimization

        var missingParentIds = missingParents.Values.Distinct().ToArray();

        var potentialParents = await LoadPortfolioParentsByExternalIds(
            workspace.OwnershipInfo.SystemId!,
            missingParentIds,
            cancellationToken);

        if (potentialParents.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("No potential parents found for missing parent external IDs.");
            return;
        }

        var workItemIdsMissingParents = missingParents.Keys.ToArray();

        if (workItemIdsMissingParents.Length == 0)
            return;

        foreach (var batch in workItemIdsMissingParents.Chunk(batchSize))
        {
            // Load only the work items that have valid parents available
            // This reduces the number of entities loaded when many parents are missing
            var validExternalIds = batch
                .Where(extId => missingParents.TryGetValue(extId, out var parentExtId) && potentialParents.ContainsKey(parentExtId))
                .ToArray();

            if (validExternalIds.Length == 0)
                continue;

            var missingParentWorkItems = await _workDbContext.WorkItems
                .Include(w => w.Type)
                    .ThenInclude(t => t.Level)
                .Where(w => w.WorkspaceId == workspace.Id
                    && w.ExternalId != null
                    && validExternalIds.Contains(w.ExternalId.Value))
                .ToListAsync(cancellationToken);

            foreach (var workItem in missingParentWorkItems)
            {
                if (!workItem.ExternalId.HasValue)
                    continue;

                var missingParentId = missingParents[workItem.ExternalId.Value];

                if (potentialParents.TryGetValue(missingParentId, out var parentInfo))
                {
                    var result = workItem.UpdateParent(parentInfo, workItem.Type);
                    if (result.IsFailure && _logger.IsEnabled(LogLevel.Debug))
                    {
                        if (workItem.ParentId.HasValue)
                        {
                            _logger.LogDebug("Broken parent link for work item {ExternalId} in workspace {WorkspaceId}. {Error}", workItem.ExternalId, workspace.Id, result.Error);
                        }
                        else
                        {
                            _logger.LogDebug("Unable to map parent for work item {ExternalId} in workspace {WorkspaceId}. {Error}", workItem.ExternalId, workspace.Id, result.Error);
                        }
                    }
                }
            }

            if (missingParentWorkItems.Count > 0)
            {
                await _workDbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task SyncParentProjectIds(Workspace workspace, HashSet<WorkType> workTypes, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Syncing parent project ids for workspace {WorkspaceId}.", workspace.Id);

        if (workTypes.Any(t => t.Level is null))
        {
            _logger.LogError("Unable to sync parent project ids for workspace {WorkspaceId} because one or more work types do not have a level.", workspace.Id);
            return;
        }

        int updateCount = 0;

        // WorkTypeTier.Other tier is not a valid target for project ids
        WorkTypeTier[] tiers = [WorkTypeTier.Portfolio, WorkTypeTier.Requirement, WorkTypeTier.Task];
        var topTierLevelSkipped = false;
        foreach (var tier in tiers)
        {
            var types = workTypes
                .Where(t => t.Level != null && t.Level.Tier == tier)
                .OrderBy(t => t.Level!.Order);

            foreach (var workType in types)
            {
                if (!topTierLevelSkipped)
                {
                    topTierLevelSkipped = true;
                    continue;
                }

                // First, get only the IDs and parent info we need with a lightweight projection query
                // This avoids loading full entities with navigation properties
                var workItemsNeedingUpdate = await _workDbContext.WorkItems
                    .Where(wi => wi.WorkspaceId == workspace.Id
                        && wi.TypeId == workType.Id
                        && wi.Parent != null
                        && wi.ParentProjectId != (wi.Parent.ProjectId ?? wi.Parent.ParentProjectId))
                    .Select(wi => new
                    {
                        wi.Id,
                        wi.ExternalId,
                        ParentInfo = new WorkItemParentInfo
                        {
                            Id = wi.Parent!.Id,
                            ExternalId = wi.Parent.ExternalId,
                            Tier = wi.Parent.Type.Level!.Tier,
                            LevelOrder = wi.Parent.Type.Level.Order,
                            ProjectId = wi.Parent.ProjectId ?? wi.Parent.ParentProjectId
                        }
                    })
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                if (workItemsNeedingUpdate.Count == 0)
                    continue;

                // Now load only the entities that need updating (without Parent navigation to avoid circular reference)
                var workItemIds = workItemsNeedingUpdate.Select(x => x.Id).ToArray();
                var workItems = await _workDbContext.WorkItems
                    .Include(wi => wi.Type)
                        .ThenInclude(t => t.Level)
                    .Where(wi => workItemIds.Contains(wi.Id))
                    .ToListAsync(cancellationToken);

                var workItemDict = workItems.ToDictionary(wi => wi.Id);

                int typeUpdateCount = 0;
                foreach (var item in workItemsNeedingUpdate)
                {
                    if (workItemDict.TryGetValue(item.Id, out var workItem))
                    {
                        // Use domain method to update parent which respects business rules
                        var result = workItem.UpdateParent(item.ParentInfo, workType);
                        if (result.IsSuccess)
                        {
                            typeUpdateCount++;
                        }
                        else if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("Unable to update parent project id for work item {ExternalId} in workspace {WorkspaceId}. {Error}",
                                item.ExternalId, workspace.Id, result.Error);
                        }
                    }
                }

                if (typeUpdateCount > 0)
                {
                    await _workDbContext.SaveChangesAsync(cancellationToken);
                    updateCount += typeUpdateCount;

                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Updated {Count} parent project ids for work type {WorkTypeId} in workspace {WorkspaceId}.",
                            typeUpdateCount, workType.Id, workspace.Id);
                }
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Synced {UpdateCount} parent project ids for workspace {WorkspaceId}.", updateCount, workspace.Id);
    }

    private sealed record WorkStatusMapping(int WorkTypeId, WorkStatus WorkStatus, WorkStatusCategory WorkStatusCategory);

    private sealed record EmployeeIds(Guid? CreatedById, Guid? LastModifiedById, Guid? AssignedToId);

    private sealed record WorkItemSyncLog
    {
        public WorkItemSyncLog(int requested)
        {
            Requested = requested;
        }

        public int Requested { get; init; }
        public int Created { get; private set; }
        public int Updated { get; private set; }
        public int Errors { get; private set; }
        public int Processed => Created + Updated;
        public int? LastExternalId { get; private set; }

        public void ItemRequested(int externalId)
        {
            LastExternalId = externalId;
        }

        public void ItemCreated()
        {
            Created++;
            LastExternalId = null;
        }

        public void ItemUpdated()
        {
            Updated++;
            LastExternalId = null;
        }

        public void ItemError()
        {
            Errors++;
            LastExternalId = null;
        }
    }
}
