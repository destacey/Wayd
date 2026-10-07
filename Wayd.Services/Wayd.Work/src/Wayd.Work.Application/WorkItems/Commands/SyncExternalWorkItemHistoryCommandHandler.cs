using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItems.Commands;

/// <summary>
/// Stores a batch of source revisions in the <see cref="WorkItemSourceRevision"/> log, brings each
/// affected item's <see cref="WorkItemStateHistory"/> up to date, and saves the batch's watermark in
/// the same transaction, so a failed batch is retried from where the last stored one ended.
/// </summary>
/// <remarks>
/// A revision after every one already stored for its item extends the item's periods. One that
/// lands before a stored revision, as happens when an item moved between projects and each
/// project's revisions are read separately, rebuilds the item's periods from its log, since a
/// period cannot say where inside it a late revision belongs.
/// <para>
/// Each value is resolved the way the work item sync resolves it, and the source's value is kept
/// beside the result. Revisions of an item Wayd does not hold in any workspace of the source system
/// are skipped: history is deleted with its work item, and the work item sync runs first, so a
/// missing item is one that was deleted. A revision already stored is skipped, so a full sync
/// replays from the start and fills gaps without changing what is stored.
/// </para>
/// </remarks>
public sealed class SyncExternalWorkItemHistoryCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider, ILogger<SyncExternalWorkItemHistoryCommandHandler> logger) : ICommandHandler<SyncExternalWorkItemHistoryCommand, int>
{
    // Keeps IN lists inside SQL Server's parameter limit.
    private const int LookupBatchSize = 1000;

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<SyncExternalWorkItemHistoryCommandHandler> _logger = logger;

    public async Task<Result<int>> Handle(SyncExternalWorkItemHistoryCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _workDbContext.Workspaces
            .FirstOrDefaultAsync(w => w.Id == request.WorkspaceId && w.OwnershipInfo.Ownership == Ownership.Managed, cancellationToken);
        if (workspace is null)
            return Result.Failure<int>($"Unable to sync the work item history of workspace {request.WorkspaceId} because the workspace does not exist.");

        await using var unitOfWork = await _workDbContext.BeginUnitOfWork(cancellationToken);

        var written = 0;
        var skipped = 0;
        var stored = 0;
        var rebuilt = 0;

        var filledExternalIds = request.FilledWorkItemIds ?? [];
        var workItemIds = await LoadWorkItemIds(workspace, request.Revisions.Select(r => r.WorkItemId).Concat(filledExternalIds), cancellationToken);
        List<WorkItemSourceRevision> newRevisions = [];

        if (request.Revisions.Count > 0)
        {
            skipped = request.Revisions.Count(r => !workItemIds.ContainsKey(r.WorkItemId));

            var incoming = request.Revisions
                .Where(r => workItemIds.ContainsKey(r.WorkItemId))
                .DistinctBy(r => (r.WorkItemId, r.Revision))
                .Select(r => ToSourceRevision(r, workItemIds[r.WorkItemId], workspace.Id))
                .ToList();

            var lastStored = await LoadLastStoredRevisions(incoming, cancellationToken);
            var alreadyStored = await LoadStoredRevisionKeys(incoming, cancellationToken);
            newRevisions = incoming.Where(r => !alreadyStored.Contains((r.WorkItemId, r.Revision))).ToList();

            if (newRevisions.Count > 0)
            {
                await _workDbContext.WorkItemSourceRevisions.AddRangeAsync(newRevisions, cancellationToken);
                stored = newRevisions.Count;

                var byItem = newRevisions.GroupBy(r => r.WorkItemId).ToList();
                var rebuildIds = byItem
                    .Where(g => g.Min(r => r.Revision) < lastStored.GetValueOrDefault(g.Key))
                    .Select(g => g.Key)
                    .ToHashSet();
                var appended = byItem
                    .Where(g => !rebuildIds.Contains(g.Key))
                    .SelectMany(g => g)
                    .ToList();

                var rebuildRevisions = await LoadSourceRevisions(rebuildIds, cancellationToken);
                rebuildRevisions.AddRange(newRevisions.Where(r => rebuildIds.Contains(r.WorkItemId)));

                var allValues = appended.Concat(rebuildRevisions).Select(r => r.Values).ToList();
                var statuses = await LoadStatusResolver(workspace.WorkProcessId, cancellationToken);
                var iterationIds = await LoadIterationIds(workspace.OwnershipInfo.SystemId, allValues, cancellationToken);
                var employeeIds = await LoadEmployeeIds(request.ConnectionId, allValues, cancellationToken);
                var resolve = (WorkItemSourceValues values) => Resolve(values, statuses, iterationIds, employeeIds);

                var newPeriods = new List<WorkItemStateHistory>();

                var timelines = await LoadTimelines(workspace.Id, appended.Select(r => r.WorkItemId).Distinct(), lastStored, cancellationToken);
                foreach (var revision in appended.OrderBy(r => r.WorkItemId).ThenBy(r => r.Revision))
                {
                    var period = timelines[revision.WorkItemId].Apply(revision.Revision, revision.Changed, resolve(revision.Values), revision.WorkspaceId);
                    if (period is not null)
                        newPeriods.Add(period);
                }

                if (rebuildIds.Count > 0)
                {
                    await DeletePeriods(rebuildIds, cancellationToken);
                    foreach (var item in rebuildRevisions.GroupBy(r => r.WorkItemId))
                    {
                        var timeline = new WorkItemStateTimeline(item.Key, workspace.Id, null, 0);
                        foreach (var revision in item.OrderBy(r => r.Revision))
                        {
                            var period = timeline.Apply(revision.Revision, revision.Changed, resolve(revision.Values), revision.WorkspaceId);
                            if (period is not null)
                                newPeriods.Add(period);
                        }
                    }

                    rebuilt = rebuildIds.Count;
                }

                if (newPeriods.Count > 0)
                    await _workDbContext.WorkItemStateHistory.AddRangeAsync(newPeriods, cancellationToken);

                written = newPeriods.Count;
            }
        }

        if (request.FilledWorkItemIds is null)
        {
            workspace.SetWorkItemHistoryWatermark(request.Watermark);
            if (request.IsLastBatch)
                workspace.WorkItemHistoryReadToEnd(_dateTimeProvider.Now);
        }
        else
        {
            var filled = filledExternalIds
                .Where(workItemIds.ContainsKey)
                .Select(id => workItemIds[id])
                .ToHashSet();
            await RecordFills(filled, newRevisions, cancellationToken);
        }

        await _workDbContext.SaveChangesAsync(cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Synced {RevisionCount} work item revisions for workspace {WorkspaceId}: {StoredCount} new revisions stored, {WrittenCount} periods written, {RebuiltCount} items rebuilt from out-of-order revisions, {SkippedCount} revisions of items not in Wayd skipped.",
            request.Revisions.Count, workspace.Id, stored, written, rebuilt, skipped);

        return written;
    }

    private static WorkItemSourceRevision ToSourceRevision(IExternalWorkItemRevision revision, Guid workItemId, Guid workspaceId) =>
        WorkItemSourceRevision.Create(
            workItemId,
            workspaceId,
            revision.Revision,
            revision.Changed,
            new WorkItemSourceValues(
                revision.IterationId,
                revision.WorkStatus,
                revision.WorkType,
                revision.TeamKey,
                revision.AssignedTo?.ExternalId,
                revision.StoryPoints,
                revision.Effort,
                revision.Size));

    private static WorkItemTrackedState Resolve(
        WorkItemSourceValues values,
        StatusResolver statuses,
        Dictionary<string, Guid> iterationIds,
        Dictionary<string, Guid?> employeeIds)
    {
        var (workTypeId, statusId, statusCategory) = statuses.Resolve(values.WorkTypeName, values.StatusName);

        Guid? iterationId = values.ExternalIterationId is int externalIterationId
            && iterationIds.TryGetValue(externalIterationId.ToString(), out var id)
                ? id
                : null;

        Guid? assignedToId = values.AssignedToExternalId is not null && employeeIds.TryGetValue(values.AssignedToExternalId, out var employeeId)
            ? employeeId
            : null;

        return new WorkItemTrackedState(
            iterationId,
            values.ExternalIterationId,
            statusId,
            values.StatusName,
            statusCategory,
            workTypeId,
            values.WorkTypeName,
            values.TeamKey,
            assignedToId,
            values.AssignedToExternalId,
            values.StoryPoints,
            values.Effort,
            values.Size);
    }

    /// <summary>
    /// Records the highest revision each filled item now holds, so the gap query passes over it
    /// until a newer revision arrives.
    /// </summary>
    private async Task RecordFills(HashSet<Guid> workItemIds, List<WorkItemSourceRevision> newRevisions, CancellationToken cancellationToken)
    {
        if (workItemIds.Count == 0)
            return;

        var highestStored = await LoadLastStoredRevisions(workItemIds, cancellationToken);
        var highestNew = newRevisions
            .GroupBy(r => r.WorkItemId)
            .ToDictionary(g => g.Key, g => g.Max(r => r.Revision));

        foreach (var batch in workItemIds.Chunk(LookupBatchSize))
        {
            var fills = await _workDbContext.WorkItemRevisionFills
                .Where(f => batch.Contains(f.WorkItemId))
                .ToDictionaryAsync(f => f.WorkItemId, cancellationToken);

            foreach (var workItemId in batch)
            {
                var highest = Math.Max(highestStored.GetValueOrDefault(workItemId), highestNew.GetValueOrDefault(workItemId));
                if (fills.TryGetValue(workItemId, out var fill))
                    fill.Refilled(highest);
                else
                    _workDbContext.WorkItemRevisionFills.Add(WorkItemRevisionFill.Create(workItemId, highest));
            }
        }
    }

    /// <summary>Each item's highest stored revision, from before this batch.</summary>
    private Task<Dictionary<Guid, int>> LoadLastStoredRevisions(List<WorkItemSourceRevision> incoming, CancellationToken cancellationToken) =>
        LoadLastStoredRevisions(incoming.Select(r => r.WorkItemId).Distinct(), cancellationToken);

    /// <summary>Each item's highest revision saved so far; revisions added in this batch are not yet saved.</summary>
    private async Task<Dictionary<Guid, int>> LoadLastStoredRevisions(IEnumerable<Guid> workItemIds, CancellationToken cancellationToken)
    {
        var last = new Dictionary<Guid, int>();
        foreach (var batch in workItemIds.Chunk(LookupBatchSize))
        {
            var rows = await _workDbContext.WorkItemSourceRevisions
                .Where(r => batch.Contains(r.WorkItemId))
                .GroupBy(r => r.WorkItemId)
                .Select(g => new { WorkItemId = g.Key, Revision = g.Max(r => r.Revision) })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
                last[row.WorkItemId] = row.Revision;
        }

        return last;
    }

    /// <summary>The batch's revisions that are already stored, so a replay stores nothing twice.</summary>
    private async Task<HashSet<(Guid WorkItemId, int Revision)>> LoadStoredRevisionKeys(List<WorkItemSourceRevision> incoming, CancellationToken cancellationToken)
    {
        var keys = new HashSet<(Guid, int)>();
        if (incoming.Count == 0)
            return keys;

        var lowest = incoming.Min(r => r.Revision);
        var highest = incoming.Max(r => r.Revision);
        foreach (var batch in incoming.Select(r => r.WorkItemId).Distinct().Chunk(LookupBatchSize))
        {
            // A range rather than a list of revision numbers: a filled item brings its whole history,
            // and one number per parameter would pass SQL Server's limit for a long-lived item. It
            // matches more pairs than the batch holds; the set lookup below is exact.
            var rows = await _workDbContext.WorkItemSourceRevisions
                .Where(r => batch.Contains(r.WorkItemId) && r.Revision >= lowest && r.Revision <= highest)
                .Select(r => new { r.WorkItemId, r.Revision })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
                keys.Add((row.WorkItemId, row.Revision));
        }

        return keys;
    }

    private async Task<List<WorkItemSourceRevision>> LoadSourceRevisions(HashSet<Guid> workItemIds, CancellationToken cancellationToken)
    {
        var revisions = new List<WorkItemSourceRevision>();
        foreach (var batch in workItemIds.Chunk(LookupBatchSize))
        {
            revisions.AddRange(await _workDbContext.WorkItemSourceRevisions
                .AsNoTracking()
                .Where(r => batch.Contains(r.WorkItemId))
                .ToListAsync(cancellationToken));
        }

        return revisions;
    }

    /// <summary>
    /// Removes the periods of items about to be rebuilt. Set-based, ahead of the save that inserts
    /// their replacements, so the unique open-period and revision indexes never see both at once.
    /// </summary>
    private async Task DeletePeriods(HashSet<Guid> workItemIds, CancellationToken cancellationToken)
    {
        foreach (var batch in workItemIds.Chunk(LookupBatchSize))
        {
            await _workDbContext.WorkItemStateHistory
                .Where(h => batch.Contains(h.WorkItemId))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    /// <summary>
    /// The Wayd id of each referenced work item, matched across every workspace of the source
    /// system rather than the one being synced: an item that has since moved to another workspace
    /// still owns the revisions it made before the move.
    /// </summary>
    private async Task<Dictionary<int, Guid>> LoadWorkItemIds(Workspace workspace, IEnumerable<int> externalIds, CancellationToken cancellationToken)
    {
        var systemId = workspace.OwnershipInfo.SystemId;
        var workspaceId = workspace.Id;
        var inSystem = systemId is null
            ? _workDbContext.WorkItems.Where(w => w.WorkspaceId == workspaceId)
            : _workDbContext.WorkItems.Where(w => w.Workspace.OwnershipInfo.SystemId == systemId);

        var ids = new Dictionary<int, Guid>();
        foreach (var batch in externalIds.Distinct().Chunk(LookupBatchSize))
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

    private async Task<Dictionary<string, Guid>> LoadIterationIds(string? systemId, List<WorkItemSourceValues> values, CancellationToken cancellationToken)
    {
        var ids = new Dictionary<string, Guid>();
        if (systemId is null)
            return ids;

        var externalIds = values
            .Where(v => v.ExternalIterationId.HasValue)
            .Select(v => v.ExternalIterationId!.Value.ToString())
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
    private async Task<Dictionary<string, Guid?>> LoadEmployeeIds(Guid connectionId, List<WorkItemSourceValues> values, CancellationToken cancellationToken)
    {
        var ids = new Dictionary<string, Guid?>(StringComparer.Ordinal);

        var externalIds = values
            .Select(v => v.AssignedToExternalId)
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

    /// <param name="lastStored">
    /// Each item's highest stored revision. Read from the log rather than the periods: a revision
    /// that changed nothing tracked opened no period but was still applied.
    /// </param>
    private async Task<Dictionary<Guid, WorkItemStateTimeline>> LoadTimelines(Guid workspaceId, IEnumerable<Guid> workItemIds, Dictionary<Guid, int> lastStored, CancellationToken cancellationToken)
    {
        var timelines = new Dictionary<Guid, WorkItemStateTimeline>();

        foreach (var batch in workItemIds.Chunk(LookupBatchSize))
        {
            // Tracked: applying a revision closes the open period, and that has to be saved.
            var openPeriods = await _workDbContext.WorkItemStateHistory
                .Where(h => batch.Contains(h.WorkItemId) && h.ValidTo == null)
                .ToDictionaryAsync(h => h.WorkItemId, cancellationToken);

            foreach (var workItemId in batch)
            {
                timelines[workItemId] = new WorkItemStateTimeline(
                    workItemId,
                    workspaceId,
                    openPeriods.GetValueOrDefault(workItemId),
                    lastStored.GetValueOrDefault(workItemId));
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
