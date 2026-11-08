using Wayd.Common.Application.StatusWorkflows;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItems;

/// <summary>
/// Turns the values a source recorded in a revision into what Wayd holds for them: the work type, the status in
/// that type's workflow, the iteration and the assignee. The values the source recorded are kept beside each one.
/// </summary>
/// <remarks>
/// Shared by the history sync and the history rebuild, so a period reads the same however it was written.
/// <para>
/// A status resolves within the workflow the revision's own type followed in the workspace that read the revision
/// (<see cref="WorkItemSourceRevision.WorkspaceId"/>), retired statuses included. An item that moved between
/// workspaces on different processes therefore resolves its earlier periods against the process it was in, and a
/// type change part way through its history resolves each period against the type it had. Nothing falls back to a
/// status of another workflow: a status with no match keeps the source's name, with no status and no category.
/// </para>
/// </remarks>
public sealed class WorkItemRevisionResolver
{
    // Keeps IN lists inside SQL Server's parameter limit.
    private const int LookupBatchSize = 1000;

    private readonly IWorkDbContext _dbContext;
    private readonly IStatusResolver _statusResolver;
    private readonly Workspace _workspace;
    private readonly Guid _connectionId;

    /// <summary>Each workspace's statuses, loaded once and shared by every batch.</summary>
    private readonly Dictionary<Guid, WorkspaceStatuses> _workspaces;

    private readonly Dictionary<string, Guid> _iterationIds;
    private readonly Dictionary<string, Guid?> _employeeIds;

    private WorkItemRevisionResolver(
        IWorkDbContext dbContext,
        IStatusResolver statusResolver,
        Workspace workspace,
        Guid connectionId,
        Dictionary<Guid, WorkspaceStatuses> workspaces,
        Dictionary<string, Guid> iterationIds,
        Dictionary<string, Guid?> employeeIds)
    {
        _dbContext = dbContext;
        _statusResolver = statusResolver;
        _workspace = workspace;
        _connectionId = connectionId;
        _workspaces = workspaces;
        _iterationIds = iterationIds;
        _employeeIds = employeeIds;
    }

    /// <summary>
    /// Loads the statuses of <paramref name="workspace"/>; <see cref="For"/> then readies the resolver for a batch
    /// of revisions.
    /// </summary>
    /// <param name="workspace">The workspace whose sync or rebuild this is.</param>
    /// <param name="connectionId">The connection whose identity mappings name the assignees.</param>
    /// <returns>
    /// A failure when <paramref name="workspace"/> has no status workflow assigned. Another workspace a revision
    /// was read by that has none leaves that revision's status unmatched: it is not the one being synced.
    /// </returns>
    public static async Task<Result<WorkItemRevisionResolver>> Load(
        IWorkDbContext dbContext,
        IStatusResolver statusResolver,
        Workspace workspace,
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        Guard.Against.Null(workspace, nameof(workspace));

        var (own, error) = await WorkspaceStatuses.Load(dbContext, statusResolver, workspace.Id, cancellationToken);

        return error is null
            ? new WorkItemRevisionResolver(dbContext, statusResolver, workspace, connectionId, new() { [workspace.Id] = own }, [], [])
            : Result.Failure<WorkItemRevisionResolver>(error);
    }

    /// <summary>
    /// The resolver for a batch of revisions: the statuses already loaded are reused, and only the workspaces,
    /// iterations and people the batch adds are loaded.
    /// </summary>
    public async Task<WorkItemRevisionResolver> For(IReadOnlyCollection<WorkItemSourceRevision> revisions, CancellationToken cancellationToken)
    {
        Guard.Against.Null(revisions, nameof(revisions));

        foreach (var workspaceId in revisions.Select(r => r.WorkspaceId).Distinct().Where(id => !_workspaces.ContainsKey(id)))
        {
            (_workspaces[workspaceId], _) = await WorkspaceStatuses.Load(_dbContext, _statusResolver, workspaceId, cancellationToken);
        }

        var values = revisions.Select(r => r.Values).ToList();
        var iterationIds = await LoadIterationIds(_dbContext, _workspace.OwnershipInfo.SystemId, values, cancellationToken);
        var employeeIds = await LoadEmployeeIds(_dbContext, _connectionId, values, cancellationToken);

        return new WorkItemRevisionResolver(_dbContext, _statusResolver, _workspace, _connectionId, _workspaces, iterationIds, employeeIds);
    }

    /// <summary>The tracked state the revision's values resolve to.</summary>
    /// <exception cref="InvalidOperationException">The revision was not among those the resolver was loaded for.</exception>
    public WorkItemTrackedState Resolve(WorkItemSourceRevision revision)
    {
        Guard.Against.Null(revision, nameof(revision));

        if (!_workspaces.TryGetValue(revision.WorkspaceId, out var workspace))
        {
            throw new InvalidOperationException($"The resolver was not loaded for workspace {revision.WorkspaceId}.");
        }

        var values = revision.Values;
        int? workTypeId = workspace.WorkTypeIds.TryGetValue(values.WorkTypeName, out var typeId) ? typeId : null;
        var status = workTypeId is int id ? workspace.Statuses.Find(id, values.StatusName) : null;

        Guid? iterationId = values.ExternalIterationId is int externalIterationId
            && _iterationIds.TryGetValue(externalIterationId.ToString(), out var iteration)
                ? iteration
                : null;

        Guid? assignedToId = values.AssignedToExternalId is not null && _employeeIds.TryGetValue(values.AssignedToExternalId, out var employeeId)
            ? employeeId
            : null;

        return new WorkItemTrackedState(
            iterationId,
            values.ExternalIterationId,
            status?.StatusId,
            values.StatusName,
            status?.Category,
            workTypeId,
            values.WorkTypeName,
            values.TeamKey,
            assignedToId,
            values.AssignedToExternalId,
            values.StoryPoints,
            values.Effort,
            values.Size);
    }

    /// <summary>One workspace's work types by name, and the statuses each can hold there.</summary>
    private sealed record WorkspaceStatuses(Dictionary<string, int> WorkTypeIds, WorkTypeStatuses Statuses)
    {
        /// <summary>
        /// Loads the workspace's work types and their statuses. When its workflows cannot be resolved, its types
        /// still are and its statuses match nothing, with the reason as the error.
        /// </summary>
        public static async Task<(WorkspaceStatuses Statuses, string? Error)> Load(
            IWorkDbContext dbContext, IStatusResolver statusResolver, Guid workspaceId, CancellationToken cancellationToken)
        {
            // Unfiltered, then filtered again below the workspace: one deleted since it read a revision still names
            // the types its process has.
            var workTypes = await dbContext.Workspaces
                .IgnoreQueryFilters()
                .Where(w => w.Id == workspaceId)
                .SelectMany(w => w.WorkProcess!.Schemes)
                .Where(s => !s.IsDeleted && s.WorkType != null && !s.WorkType.IsDeleted)
                .Select(s => new { s.WorkType!.Id, s.WorkType.Name, s.WorkType.RecordTypeId })
                .ToListAsync(cancellationToken);

            var workTypeIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var workType in workTypes)
            {
                workTypeIds.TryAdd(workType.Name, workType.Id);
            }

            var statuses = await WorkTypeStatuses.Load(
                statusResolver, workspaceId, workTypes.Select(t => (t.Id, t.RecordTypeId)), cancellationToken);

            return statuses.IsSuccess
                ? (new WorkspaceStatuses(workTypeIds, statuses.Value), null)
                : (new WorkspaceStatuses(workTypeIds, WorkTypeStatuses.Empty), statuses.Error);
        }
    }

    private static async Task<Dictionary<string, Guid>> LoadIterationIds(
        IWorkDbContext dbContext, string? systemId, IReadOnlyCollection<WorkItemSourceValues> values, CancellationToken cancellationToken)
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
            var rows = await dbContext.Iterations
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
    private static async Task<Dictionary<string, Guid?>> LoadEmployeeIds(
        IWorkDbContext dbContext, Guid connectionId, IReadOnlyCollection<WorkItemSourceValues> values, CancellationToken cancellationToken)
    {
        var ids = new Dictionary<string, Guid?>(StringComparer.Ordinal);

        var externalIds = values
            .Select(v => v.AssignedToExternalId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal);

        foreach (var batch in externalIds.Chunk(LookupBatchSize))
        {
            var rows = await dbContext.ExternalIdentityMappings
                .Where(m => m.ConnectionId == connectionId && batch.Contains(m.ExternalId))
                .Select(m => new { m.ExternalId, m.EmployeeId })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
                ids[row.ExternalId] = row.EmployeeId;
        }

        return ids;
    }
}
