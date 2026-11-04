using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// An aggregate whose status a source system decides: a record synced from a managed workflow, which
/// takes the status its source reports.
/// </summary>
/// <remarks>
/// The source has already made the move, so Wayd neither checks it against the workflow's transitions nor
/// records a <see cref="StatusTransition"/>. A transition row would be wrong on every count: the actor is a
/// source identity rather than a Wayd one, a late revision can rebuild history that appended rows cannot be
/// rewritten to match, and the record's own synced history (a work item's state history) already holds the
/// change. Moves Wayd decides belong to <see cref="StatusTrackedEntity"/>, which keeps both.
/// <para>
/// The stored status is a copy of the workflow's current one, not a value frozen when applied: a managed
/// workflow is published from the start, so a rename from the source or a category or alias change by an
/// administrator reaches records already holding the status, and the owner type re-applies it to them
/// (<see cref="WorkflowOwnerDescriptor.HasManagedWorkflows"/>).
/// </para>
/// <para>
/// Every type deriving from this is listed in the architecture tests' allow-list, so the one path that
/// skips transitions is taken on purpose.
/// </para>
/// </remarks>
public abstract class ManagedStatusEntity : StatusHoldingEntity
{
    /// <summary>
    /// Takes the status the source reports, or the current state of the status already held.
    /// </summary>
    /// <returns>
    /// <c>true</c> when anything the record stores about its status changed — the status, its workflow, or
    /// its name, category or alias; <c>false</c> when it already matched.
    /// </returns>
    /// <remarks>
    /// Any restriction <paramref name="status"/> carries is ignored, and so is its workflow: a record whose
    /// type changed in the source lands in the new type's workflow by this same call.
    /// </remarks>
    protected bool ApplySourceStatus(StatusRef status)
    {
        Guard.Against.Null(status, nameof(status));

        if (HasStatus
            && status.StatusId == StatusId
            && status.WorkflowId == StatusWorkflowId
            && status.Name == StatusName
            && status.Category == StatusCategory
            && status.Alias == StatusAliasValue)
        {
            return false;
        }

        SetStatus(status);

        return true;
    }

    /// <summary>
    /// Moves this record onto the workflow a remap targets, translating its current status, with no
    /// transition recorded.
    /// </summary>
    /// <returns>
    /// A failure when the remap still has unmapped statuses, or when the record holds a status from neither
    /// of its workflows. A record already on the target workflow succeeds without change, so a batched
    /// migration can re-run.
    /// </returns>
    /// <remarks>
    /// For a scope whose assignment or scheme changes, moving every record in it. The managed counterpart of
    /// <see cref="StatusTrackedEntity.SwitchWorkflow"/>.
    /// </remarks>
    public Result ApplyRemap(StatusRemap remap)
    {
        Guard.Against.Null(remap, nameof(remap));

        if (!remap.IsComplete)
        {
            return Result.Failure("Every status must be mapped before records can be moved.");
        }

        var target = remap.For(StatusId);
        if (target is null)
        {
            return remap.ToWorkflowId == StatusWorkflowId
                ? Result.Success()
                : Result.Failure("This record's status is not in the workflow being moved from.");
        }

        ApplySourceStatus(target);

        return Result.Success();
    }
}
