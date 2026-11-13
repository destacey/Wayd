using NodaTime;

namespace Wayd.Work.Domain.Models.SprintScope;

/// <summary>A work item that was in a sprint's scope: how it came in, and what became of it.</summary>
/// <param name="AddedAt">
/// When the item was added to the sprint: for an added item, when it entered during the sprint; for a committed
/// one, when it was put in the sprint before the commitment point.
/// </param>
/// <param name="RemovedAt">
/// When the item was last removed from the sprint — moved to another iteration or out of the requirement tier;
/// not when it reached a Removed status. Null if it was still in at the effective end, or is still in a sprint
/// that has not ended.
/// </param>
/// <param name="EntryEstimate">The item's estimate when it was committed or added.</param>
/// <param name="OutcomeEstimate">The item's estimate when it was last in the sprint.</param>
public sealed record SprintScopeItem(
    Guid WorkItemId,
    SprintScopeEntry Entry,
    Instant AddedAt,
    SprintScopeOutcome Outcome,
    Instant? RemovedAt,
    double? EntryEstimate,
    double? OutcomeEstimate)
{
    /// <summary>Whether the item counts as done: completed, or completed as Removed.</summary>
    public bool IsCompleted => Outcome is SprintScopeOutcome.Completed or SprintScopeOutcome.Removed;
}
