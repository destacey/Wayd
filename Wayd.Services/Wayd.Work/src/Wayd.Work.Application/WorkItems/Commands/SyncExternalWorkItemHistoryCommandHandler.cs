using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItems.Commands;

/// <summary>
/// Applies a batch of source revisions to the workspace's <see cref="WorkItemStateHistory"/> and
/// saves the batch's watermark with it, so a failed batch is retried from where the last stored
/// one ended.
/// </summary>
/// <remarks>
/// Each value is resolved the way the work item sync resolves it, and the source's value is kept
/// beside the result. Revisions of an item Wayd does not hold in any workspace of the source system
/// are skipped: history is deleted with its work item, and the work item sync runs first, so a
/// missing item is one that was deleted.
/// <para>
/// Nothing here deletes history. A full sync replays from the start and relies on already-applied
/// revisions being skipped, so it fills gaps without touching what is stored.
/// </para>
/// </remarks>
public sealed class SyncExternalWorkItemHistoryCommandHandler(IWorkDbContext workDbContext, ILogger<SyncExternalWorkItemHistoryCommandHandler> logger) : ICommandHandler<SyncExternalWorkItemHistoryCommand>
{
    // Keeps IN lists inside SQL Server's parameter limit.
    private const int LookupBatchSize = 1000;

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<SyncExternalWorkItemHistoryCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncExternalWorkItemHistoryCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _workDbContext.Workspaces
            .FirstOrDefaultAsync(w => w.Id == request.WorkspaceId && w.OwnershipInfo.Ownership == Ownership.Managed, cancellationToken);
        if (workspace is null)
            return Result.Failure($"Unable to sync the work item history of workspace {request.WorkspaceId} because the workspace does not exist.");

        var opened = 0;
        var skipped = 0;

        if (request.Revisions.Count > 0)
        {
            var workItemIds = await LoadWorkItemIds(workspace, request.Revisions, cancellationToken);
            var statuses = await LoadStatusResolver(workspace.WorkProcessId, cancellationToken);
            var iterationIds = await LoadIterationIds(workspace.OwnershipInfo.SystemId, request.Revisions, cancellationToken);
            var employeeIds = await LoadEmployeeIds(request.ConnectionId, request.Revisions, cancellationToken);
            var timelines = await LoadTimelines(workspace.Id, workItemIds.Values, cancellationToken);

            var newPeriods = new List<WorkItemStateHistory>();

            foreach (var revision in request.Revisions.OrderBy(r => r.WorkItemId).ThenBy(r => r.Revision))
            {
                if (!workItemIds.TryGetValue(revision.WorkItemId, out var workItemId))
                {
                    skipped++;
                    continue;
                }

                var state = Resolve(revision, statuses, iterationIds, employeeIds);
                var period = timelines[workItemId].Apply(revision.Revision, revision.Changed, state);
                if (period is not null)
                    newPeriods.Add(period);
            }

            if (newPeriods.Count > 0)
                await _workDbContext.WorkItemStateHistory.AddRangeAsync(newPeriods, cancellationToken);

            opened = newPeriods.Count;
        }

        workspace.SetWorkItemHistoryWatermark(request.Watermark);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Synced {RevisionCount} work item revisions for workspace {WorkspaceId}: {OpenedCount} periods opened, {SkippedCount} revisions of items not in Wayd skipped.",
            request.Revisions.Count, workspace.Id, opened, skipped);

        return Result.Success();
    }

    private static WorkItemTrackedState Resolve(
        IExternalWorkItemRevision revision,
        StatusResolver statuses,
        Dictionary<string, Guid> iterationIds,
        Dictionary<string, Guid?> employeeIds)
    {
        var (workTypeId, statusId, statusCategory) = statuses.Resolve(revision.WorkType, revision.WorkStatus);

        Guid? iterationId = revision.IterationId is int externalIterationId
            && iterationIds.TryGetValue(externalIterationId.ToString(), out var id)
                ? id
                : null;

        var assignedToExternalId = revision.AssignedTo?.ExternalId;
        Guid? assignedToId = assignedToExternalId is not null && employeeIds.TryGetValue(assignedToExternalId, out var employeeId)
            ? employeeId
            : null;

        return new WorkItemTrackedState(
            iterationId,
            revision.IterationId,
            statusId,
            revision.WorkStatus,
            statusCategory,
            workTypeId,
            revision.WorkType,
            revision.TeamKey,
            assignedToId,
            assignedToExternalId,
            revision.StoryPoints,
            revision.Effort,
            revision.Size);
    }

    /// <summary>
    /// The Wayd id of each referenced work item, matched across every workspace of the source
    /// system rather than the one being synced: an item that has since moved to another workspace
    /// still owns the revisions it made before the move.
    /// </summary>
    private async Task<Dictionary<int, Guid>> LoadWorkItemIds(Workspace workspace, IReadOnlyList<IExternalWorkItemRevision> revisions, CancellationToken cancellationToken)
    {
        var systemId = workspace.OwnershipInfo.SystemId;
        var workspaceId = workspace.Id;
        var inSystem = systemId is null
            ? _workDbContext.WorkItems.Where(w => w.WorkspaceId == workspaceId)
            : _workDbContext.WorkItems.Where(w => w.Workspace.OwnershipInfo.SystemId == systemId);

        var ids = new Dictionary<int, Guid>();
        foreach (var batch in revisions.Select(r => r.WorkItemId).Distinct().Chunk(LookupBatchSize))
        {
            var rows = await inSystem
                .Where(w => w.ExternalId != null && batch.Contains(w.ExternalId.Value))
                .Select(w => new { ExternalId = w.ExternalId!.Value, w.Id })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
                ids[row.ExternalId] = row.Id;
        }

        return ids;
    }

    private async Task<Dictionary<string, Guid>> LoadIterationIds(string? systemId, IReadOnlyList<IExternalWorkItemRevision> revisions, CancellationToken cancellationToken)
    {
        var ids = new Dictionary<string, Guid>();
        if (systemId is null)
            return ids;

        var externalIds = revisions
            .Where(r => r.IterationId.HasValue)
            .Select(r => r.IterationId!.Value.ToString())
            .Distinct();

        foreach (var batch in externalIds.Chunk(LookupBatchSize))
        {
            var rows = await _workDbContext.Iterations
                .Where(i => i.OwnershipInfo.SystemId == systemId && batch.Contains(i.OwnershipInfo.ExternalId!))
                .Select(i => new { ExternalId = i.OwnershipInfo.ExternalId!, i.Id })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
                ids[row.ExternalId] = row.Id;
        }

        return ids;
    }

    /// <summary>
    /// The employee each referenced identity is mapped to. Read only: a revision reports a person as
    /// they were then, so refreshing a mapping's details from it would overwrite current ones.
    /// </summary>
    private async Task<Dictionary<string, Guid?>> LoadEmployeeIds(Guid connectionId, IReadOnlyList<IExternalWorkItemRevision> revisions, CancellationToken cancellationToken)
    {
        var ids = new Dictionary<string, Guid?>(StringComparer.Ordinal);

        var externalIds = revisions
            .Select(r => r.AssignedTo?.ExternalId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal);

        foreach (var batch in externalIds.Chunk(LookupBatchSize))
        {
            var rows = await _workDbContext.ExternalIdentityMappings
                .Where(m => m.ConnectionId == connectionId && batch.Contains(m.ExternalId))
                .Select(m => new { m.ExternalId, m.EmployeeId })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
                ids[row.ExternalId] = row.EmployeeId;
        }

        return ids;
    }

    private async Task<Dictionary<Guid, WorkItemStateTimeline>> LoadTimelines(Guid workspaceId, IEnumerable<Guid> workItemIds, CancellationToken cancellationToken)
    {
        var timelines = new Dictionary<Guid, WorkItemStateTimeline>();

        foreach (var batch in workItemIds.Chunk(LookupBatchSize))
        {
            // Tracked: applying a revision closes the open period, and that has to be saved.
            var openPeriods = await _workDbContext.WorkItemStateHistory
                .Where(h => batch.Contains(h.WorkItemId) && h.ValidTo == null)
                .ToDictionaryAsync(h => h.WorkItemId, cancellationToken);

            var lastRevisions = await _workDbContext.WorkItemStateHistory
                .Where(h => batch.Contains(h.WorkItemId))
                .GroupBy(h => h.WorkItemId)
                .Select(g => new { WorkItemId = g.Key, Revision = g.Max(h => h.Revision) })
                .ToDictionaryAsync(r => r.WorkItemId, r => r.Revision, cancellationToken);

            foreach (var workItemId in batch)
            {
                timelines[workItemId] = new WorkItemStateTimeline(
                    workItemId,
                    workspaceId,
                    openPeriods.GetValueOrDefault(workItemId),
                    lastRevisions.GetValueOrDefault(workItemId));
            }
        }

        return timelines;
    }

    private async Task<StatusResolver> LoadStatusResolver(Guid workProcessId, CancellationToken cancellationToken)
    {
        var schemes = await _workDbContext.WorkProcesses
            .Where(p => p.Id == workProcessId)
            .SelectMany(p => p.Schemes)
            .Where(s => s.WorkType != null && s.Workflow != null)
            .Select(s => new
            {
                WorkTypeId = s.WorkType!.Id,
                WorkTypeName = s.WorkType.Name,
                Statuses = s.Workflow!.Schemes
                    .Where(ws => ws.WorkStatus != null)
                    .Select(ws => new { StatusId = ws.WorkStatus!.Id, StatusName = ws.WorkStatus.Name, ws.WorkStatusCategory })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        // Statuses are shared across workflows, so a status a type's workflow no longer holds still
        // resolves to its id by name; only its category is lost.
        var statusIdsByName = await _workDbContext.WorkStatuses
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Name, s => s.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var workTypeIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var workflowStatuses = new Dictionary<(int, string), (int Id, WorkStatusCategory Category)>(WorkflowStatusKeyComparer.Instance);
        foreach (var scheme in schemes)
        {
            workTypeIds.TryAdd(scheme.WorkTypeName, scheme.WorkTypeId);
            foreach (var status in scheme.Statuses)
                workflowStatuses.TryAdd((scheme.WorkTypeId, status.StatusName), (status.StatusId, status.WorkStatusCategory));
        }

        return new StatusResolver(workTypeIds, workflowStatuses, statusIdsByName);
    }

    /// <summary>Resolves a revision's work type and status names against the workspace's process.</summary>
    private sealed class StatusResolver(
        Dictionary<string, int> workTypeIds,
        Dictionary<(int, string), (int Id, WorkStatusCategory Category)> workflowStatuses,
        Dictionary<string, int> statusIdsByName)
    {
        public (int? WorkTypeId, int? StatusId, WorkStatusCategory? StatusCategory) Resolve(string workType, string status)
        {
            int? workTypeId = workTypeIds.TryGetValue(workType, out var typeId) ? typeId : null;

            if (workTypeId.HasValue && workflowStatuses.TryGetValue((workTypeId.Value, status), out var mapped))
                return (workTypeId, mapped.Id, mapped.Category);

            int? statusId = statusIdsByName.TryGetValue(status, out var id) ? id : null;
            return (workTypeId, statusId, null);
        }
    }

    private sealed class WorkflowStatusKeyComparer : IEqualityComparer<(int WorkTypeId, string StatusName)>
    {
        public static readonly WorkflowStatusKeyComparer Instance = new();

        public bool Equals((int WorkTypeId, string StatusName) x, (int WorkTypeId, string StatusName) y) =>
            x.WorkTypeId == y.WorkTypeId && string.Equals(x.StatusName, y.StatusName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((int WorkTypeId, string StatusName) obj) =>
            HashCode.Combine(obj.WorkTypeId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.StatusName));
    }
}
