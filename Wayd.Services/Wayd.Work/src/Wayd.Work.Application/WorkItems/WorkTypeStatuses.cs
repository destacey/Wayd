using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Work.Domain;

namespace Wayd.Work.Application.WorkItems;

/// <summary>
/// The statuses each work type's items can hold in one workspace, from the workflow the workspace's scheme gives
/// the type, retired statuses included.
/// </summary>
/// <remarks>
/// Sources report a work item's state by name, so a status is matched by name, or by a name its source renamed it
/// from, within the type's own workflow, never across workflows: two processes' "Active" are different statuses, and may be in different categories.
/// Retired statuses still match, because a managed workflow retires a state its source hides, and items and
/// revisions holding it must keep their status and category.
/// </remarks>
public sealed class WorkTypeStatuses
{
    private readonly Dictionary<int, Dictionary<string, StatusRef>> _byWorkType;

    private WorkTypeStatuses(Dictionary<int, Dictionary<string, StatusRef>> byWorkType)
    {
        _byWorkType = byWorkType;
    }

    /// <summary>No statuses for any work type, so every lookup finds nothing.</summary>
    public static WorkTypeStatuses Empty { get; } = new([]);

    /// <summary>
    /// Loads the workflow of each work type in the workspace.
    /// </summary>
    /// <param name="workTypes">The work types, each with the engine record type it is linked to, if any.</param>
    /// <returns>
    /// A failure when the workspace has no workflow assigned at all, found by the first linked type; with no type
    /// linked to a record type, nothing is resolved and every lookup finds nothing. A type with no record type, or
    /// with no entry of its own in the workspace's scheme, gets no statuses: the scheme's default belongs to
    /// another type, so borrowing it would give the type's items another type's statuses.
    /// </returns>
    public static async Task<Result<WorkTypeStatuses>> Load(
        IStatusResolver statusResolver,
        Guid workspaceId,
        IEnumerable<(int WorkTypeId, Guid? RecordTypeId)> workTypes,
        CancellationToken cancellationToken)
    {
        Guard.Against.Null(statusResolver, nameof(statusResolver));
        Guard.Against.Null(workTypes, nameof(workTypes));

        var scopes = WorkflowScopeChain.ForScope(workspaceId);
        var byWorkType = new Dictionary<int, Dictionary<string, StatusRef>>();

        foreach (var (workTypeId, recordTypeId) in workTypes.DistinctBy(t => t.WorkTypeId))
        {
            if (recordTypeId is null)
            {
                continue;
            }

            var resolved = await statusResolver.ForRecordType(WorkWorkflowOwners.WorkItem.Key, scopes, recordTypeId, cancellationToken);
            if (resolved.IsFailure)
            {
                return Result.Failure<WorkTypeStatuses>($"Unable to resolve the status workflows of workspace {workspaceId}: {resolved.Error}");
            }

            if (!resolved.Value.Source.IsRecordTypeEntry)
            {
                continue;
            }

            byWorkType[workTypeId] = ByName(resolved.Value.Workflow);
        }

        return new WorkTypeStatuses(byWorkType);
    }

    /// <summary>
    /// The status named <paramref name="statusName"/> in the workflow of <paramref name="workTypeId"/>, or
    /// <c>null</c> when the type has no workflow or the workflow no such status.
    /// </summary>
    public StatusRef? Find(int workTypeId, string? statusName) =>
        statusName is not null
        && _byWorkType.TryGetValue(workTypeId, out var statuses)
        && statuses.TryGetValue(statusName.Trim(), out var status)
            ? status
            : null;

    /// <summary>
    /// Each status by name, then by the names its source renamed it from, so revisions written before a rename
    /// still resolve. A current name always wins over a former one, and an active status over a retired one of
    /// the same name, which a managed workflow can hold when its source hid one state and added another under the
    /// old name. A former name two statuses have both had goes to the first of them in that same order.
    /// </summary>
    private static Dictionary<string, StatusRef> ByName(StatusWorkflow workflow)
    {
        var byName = new Dictionary<string, StatusRef>(StringComparer.OrdinalIgnoreCase);
        var statuses = workflow.Statuses.OrderBy(s => s.IsRetired).ThenBy(s => s.Order).ToList();

        foreach (var status in statuses)
        {
            byName.TryAdd(status.Name, StatusRef.From(status));
        }

        foreach (var status in statuses)
        {
            foreach (var formerName in status.FormerNames)
            {
                byName.TryAdd(formerName, StatusRef.From(status));
            }
        }

        return byName;
    }
}
