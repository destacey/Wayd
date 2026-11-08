using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Domain;
using Wayd.Work.Domain.Models;

namespace Wayd.Infrastructure.StatusWorkflows;

/// <summary>
/// Moves work items between workflows when a scheme change gives their type in their workspace another one.
/// </summary>
/// <remarks>
/// A workspace is a work item's only scope, and its work type's record type is its record type, so a selection
/// names items by workspace and by their type's <see cref="WorkType.RecordTypeId"/>. A null scope is the
/// organization's assignment, which governs the items of every workspace without an assignment of its own.
/// <para>
/// A managed scheme refuses the edits that would call this, since its source decides each item's status; it
/// serves the schemes administrators author.
/// </para>
/// <para>
/// Not marked as a service — see <see cref="IStatusRecordMigrator"/> for why, and register explicitly.
/// </para>
/// </remarks>
public sealed class WorkItemStatusRecordMigrator(
    IWorkDbContext workDbContext,
    IStatusWorkflowDbContext workflowDbContext)
    : IRecordTypeStatusMigrator
{
    /// <summary>
    /// How many items are loaded per query. Every moved item stays tracked until the caller's single save, so
    /// this bounds the size of each query, not memory or the transaction.
    /// </summary>
    private const int BatchSize = 500;

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IStatusWorkflowDbContext _workflowDbContext = workflowDbContext;

    /// <inheritdoc/>
    public string OwnerType => WorkWorkflowOwners.WorkItem.Key;

    /// <inheritdoc/>
    public async Task<Result<int>> Migrate(
        StatusRemap remap, RecordSelection selection, EventActor actor, Instant timestamp, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(remap);

        var items = await Selected(remap.FromWorkflowId, selection, cancellationToken);

        var migrated = 0;
        var offset = 0;

        while (true)
        {
            // Paged rather than filtered-and-drained: moved items stay in the change tracker unsaved, so the
            // database still holds them on the old workflow and the window does not shift.
            var batch = await items
                .OrderBy(w => w.Id)
                .Skip(offset)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
                break;

            foreach (var item in batch)
            {
                var result = item.ApplyRemap(remap);
                if (result.IsFailure)
                    return Result.Failure<int>(result.Error);

                migrated++;
            }

            offset += batch.Count;
        }

        return Result.Success(migrated);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyDictionary<Guid, int>>> CountByStatus(
        Guid workflowId, RecordSelection selection, CancellationToken cancellationToken)
    {
        var items = await Selected(workflowId, selection, cancellationToken);

        var counts = await items
            .GroupBy(w => w.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyDictionary<Guid, int>>(counts.ToDictionary(c => c.StatusId, c => c.Count));
    }

    /// <summary>The items on <paramref name="workflowId"/> that the selection names.</summary>
    private async Task<IQueryable<WorkItem>> Selected(Guid workflowId, RecordSelection selection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var items = _workDbContext.WorkItems.Where(w => w.StatusWorkflowId == workflowId);

        var workspaceIds = selection.ScopeIds.Where(id => id is not null).Select(id => id!.Value).ToList();
        if (selection.ScopeIds.Contains(null))
        {
            // Read first rather than joined: the two database views are separate interfaces, and the list is one
            // row per workspace with its own assignment.
            var key = OwnerType;
            var ownWorkspaceIds = await _workflowDbContext.WorkflowAssignments
                .Where(a => a.OwnerType == key && a.ScopeId != null)
                .Select(a => a.ScopeId!.Value)
                .ToListAsync(cancellationToken);

            items = items.Where(w => workspaceIds.Contains(w.WorkspaceId) || !ownWorkspaceIds.Contains(w.WorkspaceId));
        }
        else
        {
            items = items.Where(w => workspaceIds.Contains(w.WorkspaceId));
        }

        if (selection.RecordTypeId is { } recordTypeId)
            return items.Where(w => w.Type.RecordTypeId == recordTypeId);

        var excluded = selection.ExcludedRecordTypeIds.ToList();
        return items.Where(w => w.Type.RecordTypeId == null || !excluded.Contains(w.Type.RecordTypeId.Value));
    }
}
