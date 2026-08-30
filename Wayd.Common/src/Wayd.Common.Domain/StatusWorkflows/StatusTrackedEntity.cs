using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// An aggregate whose status comes from a <see cref="StatusWorkflow"/>, with the history of every
/// change it has been through.
/// </summary>
/// <remarks>
/// The status fields live here rather than on each aggregate so that moving status and appending a
/// transition cannot come apart: <see cref="ApplyStatus"/> is the only way to change status, and it
/// always records the move. An aggregate that set the fields itself would eventually forget the
/// history on one path, which is precisely the bug this shape prevents.
/// <para>
/// A record does not store its workflow. The workflow is the container's, resolved through
/// <c>WorkflowAssignment</c>, exactly as a work item takes its process from its project — no child
/// ever diverges from its container, so a copy per record would only be a value that can drift.
/// Which workflow governed a past change is answered by <see cref="StatusTransition.WorkflowId"/>,
/// frozen per transition.
/// </para>
/// </remarks>
public abstract class StatusTrackedEntity : BaseAuditableEntity
{
    private readonly List<StatusTransition> _statusTransitions = [];

    /// <summary>
    /// The registered owner type whose workflow governs this record. Fixed per aggregate.
    /// </summary>
    public abstract string StatusOwnerType { get; }

    /// <summary>The status this record currently holds.</summary>
    public Guid StatusId { get; private set; }

    /// <summary>
    /// What that status was called when it was applied. Frozen, so renaming the status does not rewrite
    /// what this record reads as.
    /// </summary>
    public string StatusName
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(StatusName)).Trim();
    } = default!;

    /// <summary>
    /// The status's category, denormalized so reads and invariants never need the workflow loaded.
    /// </summary>
    public StatusCategory StatusCategory { get; private set; }

    /// <summary>
    /// The well-known meaning of the current status, or <see cref="StatusWorkflow.NoAlias"/>. An
    /// <c>int</c> because the meaning belongs to the consuming module; aggregates expose it through
    /// their own alias enum.
    /// </summary>
    protected int StatusAliasValue { get; private set; }

    /// <summary>
    /// Every status change this record has been through, oldest first.
    /// </summary>
    public IReadOnlyCollection<StatusTransition> StatusTransitions =>
        _statusTransitions.OrderBy(t => t.Sequence).ToList().AsReadOnly();

    /// <summary>
    /// Takes the transitions appended since the last drain, so they can be persisted.
    /// </summary>
    /// <remarks>
    /// The history is not a navigation — one table serves every owner type, so a per-aggregate foreign key
    /// on <see cref="StatusTransition.RecordId"/> is not constructible — which means nothing persists these
    /// unless something collects them. <c>BaseDbContext</c> calls this on save; draining prevents a second
    /// save from inserting the same rows again.
    /// </remarks>
    public IReadOnlyCollection<StatusTransition> DrainStatusTransitions()
    {
        var drained = _statusTransitions.OrderBy(t => t.Sequence).ToList();
        _statusTransitions.Clear();

        return drained.AsReadOnly();
    }

    /// <summary>
    /// How many transitions this record has recorded. Assigned as the next
    /// <see cref="StatusTransition.Sequence"/> so appending does not require the history to be loaded.
    /// </summary>
    public int StatusTransitionCount { get; private set; }

    /// <summary>
    /// Moves the record to a status and records the move. The only way to change status.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the status actually changed; <c>false</c> when it was already there, so a
    /// caller can skip raising an event for a no-op.
    /// </returns>
    protected bool ApplyStatus(StatusRef status, EventActor actor, Instant timestamp, string? reason = null)
    {
        Guard.Against.Null(status, nameof(status));
        Guard.Against.Null(actor, nameof(actor));

        var isInitial = StatusId == Guid.Empty;

        if (!isInitial && status.StatusId == StatusId)
        {
            return false;
        }

        // The workflow is the container's, via its assignment — a record never names its own, so the
        // previous status is reconstructed against the same workflow the incoming one came from. A
        // genuine workflow switch is a remap, which supplies both sides explicitly.
        var from = isInitial
            ? null
            : new StatusRef(status.WorkflowId, StatusId, StatusName, StatusCategory, StatusAliasValue);

        StatusId = status.StatusId;
        StatusName = status.Name;
        StatusCategory = status.Category;
        StatusAliasValue = status.Alias;

        _statusTransitions.Add(new StatusTransition(
            StatusOwnerType,
            Id,
            from,
            status,
            actor,
            timestamp,
            StatusTransitionCount,
            reason));

        StatusTransitionCount++;

        return true;
    }

    /// <summary>
    /// Moves this record onto the workflow a remap targets, translating its current status.
    /// </summary>
    /// <remarks>
    /// Applied per record rather than in bulk so a large migration can run in batches and resume: the
    /// decisions live in the <see cref="StatusRemap"/>, so nothing is recomputed and re-running over a
    /// record already moved is a no-op rather than a second transition.
    /// <para>
    /// The move is recorded like any other status change, with the transition carrying the old
    /// workflow's status and the new one's — which is what makes a switch visible in the history rather
    /// than a silent rewrite.
    /// </para>
    /// </remarks>
    public Result SwitchWorkflow(StatusRemap remap, EventActor actor, Instant timestamp, string? reason = null)
    {
        Guard.Against.Null(remap, nameof(remap));

        if (!remap.IsComplete)
        {
            return Result.Failure("Every status must be mapped before records can be moved.");
        }

        var target = remap.For(StatusId);
        if (target is null)
        {
            // Either the record is already on the target workflow, or it holds a status from neither —
            // both mean this remap cannot speak for it, and guessing would strand it silently.
            return remap.ToWorkflowId == CurrentWorkflowId
                ? Result.Success()
                : Result.Failure("This record's status is not in the workflow being moved from.");
        }

        ApplyStatus(target, actor, timestamp, reason);

        return Result.Success();
    }

    /// <summary>
    /// The workflow the most recent transition ran under, or <see cref="Guid.Empty"/> for a record with
    /// no history. Used to recognise a record a remap has already moved.
    /// </summary>
    private Guid CurrentWorkflowId =>
        _statusTransitions.Count == 0
            ? Guid.Empty
            : _statusTransitions.OrderByDescending(t => t.Sequence).First().WorkflowId;
}
