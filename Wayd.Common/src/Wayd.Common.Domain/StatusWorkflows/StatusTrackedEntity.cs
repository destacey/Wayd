using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StatusWorkflows;
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
/// A record does not store which workflow it should follow. Its container and its record type decide,
/// through the container's <c>WorkflowAssignment</c> and the scheme it points at — records of one type
/// in one container never diverge, so a choice per record would only be a value that can drift.
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
    /// The workflow that status belongs to.
    /// </summary>
    /// <remarks>
    /// Denormalized onto the record for the same reason as <see cref="StatusCategory"/>: a reader must
    /// not have to load the workflow to know which one governs this row.
    /// <para>
    /// It is also what makes a batched workflow migration resumable. Without it the current workflow
    /// had to be inferred from the newest status transition — but the history is not a navigation and
    /// <c>DrainStatusTransitions</c> empties the in-memory list on every save, so a record loaded from
    /// the database reported no workflow at all. A re-run over an already-moved record then read as
    /// "holds a status from neither workflow" and failed instead of doing nothing.
    /// </para>
    /// </remarks>
    public Guid StatusWorkflowId { get; private set; }

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
    /// <remarks>
    /// Public rather than protected so it maps as a real column and a query can read it directly.
    /// Reaching it as a shadow property with <c>EF.Property</c> instead makes a projection translate
    /// only against a real provider — it throws under LINQ-to-Objects, so any handler test running on
    /// an in-memory fake could not exercise it. Aggregates still expose it through their own alias enum;
    /// this is for the read side.
    /// </remarks>
    public int StatusAliasValue { get; private set; }

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
    /// A failure, with nothing changed, when the target's workflow does not allow a move from the current
    /// status (<see cref="StatusRef.AllowedFrom"/>), or allows it only on conditions the move does not
    /// meet (<see cref="StatusRef.ConditionsFor"/>), naming what is missing. Moving to the status already
    /// held succeeds without recording anything. A record's initial status is never checked.
    /// </returns>
    /// <remarks>
    /// The checks live here, not in each aggregate method, so no path that changes status can forget
    /// them. Callers apply the status before touching any other state, so a refusal leaves nothing to
    /// undo.
    /// </remarks>
    protected Result ApplyStatus(StatusRef status, EventActor actor, Instant timestamp, string? reason = null) =>
        ApplyStatus(status, actor, timestamp, reason, enforceTransitions: true);

    private Result ApplyStatus(StatusRef status, EventActor actor, Instant timestamp, string? reason, bool enforceTransitions)
    {
        Guard.Against.Null(status, nameof(status));
        Guard.Against.Null(actor, nameof(actor));

        var isInitial = StatusId == Guid.Empty;

        if (!isInitial && status.StatusId == StatusId)
        {
            return Result.Success();
        }

        if (enforceTransitions && !isInitial)
        {
            if (status.AllowedFrom is { } allowedFrom && !allowedFrom.Contains(StatusId))
            {
                return Result.Failure($"The workflow does not allow a move from '{StatusName}' to '{status.Name}'.");
            }

            var missing = status.ConditionsFor(StatusId)
                .Where(kind => !IsMet(kind, status, reason))
                .Order()
                .Select(kind => kind.DescribeMissing())
                .ToList();

            if (missing.Count > 0)
            {
                return Result.Failure($"Moving from '{StatusName}' to '{status.Name}' needs {string.Join(" and ", missing)}.");
            }
        }

        // The outgoing side is built from this record's own workflow, not the incoming status's. They
        // differ during a remap — the new status belongs to the workflow being moved to — and borrowing
        // the incoming id there would record the old status against a workflow it never belonged to,
        // making the one transition that explains a migration the one that misreports it.
        var from = isInitial
            ? null
            : new StatusRef(StatusWorkflowId, StatusId, StatusName, StatusCategory, StatusAliasValue);

        StatusId = status.StatusId;
        StatusWorkflowId = status.WorkflowId;
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

        return Result.Success();
    }

    /// <summary>
    /// Whether a move meets one condition: checked here when the record can, otherwise taken from what the
    /// application layer evaluated. A kind with no evaluation is unmet, so a condition nothing checks
    /// refuses the move rather than letting it through.
    /// </summary>
    private static bool IsMet(TransitionConditionKind kind, StatusRef status, string? reason) =>
        kind switch
        {
            TransitionConditionKind.RequiresReason => !string.IsNullOrWhiteSpace(reason),
            _ => !kind.IsSelfChecked() && status.MetConditions.Contains(kind),
        };

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
    /// than a silent rewrite. It raises <see cref="StatusWorkflowSwitchedEvent"/> only when the record
    /// actually moved, so a re-run stays silent.
    /// </para>
    /// </remarks>
    public Result SwitchWorkflow(StatusRemap remap, EventActor actor, Instant timestamp, string? reason = null)
    {
        Guard.Against.Null(remap, nameof(remap));
        Guard.Against.Null(actor, nameof(actor));

        if (!remap.IsComplete)
        {
            return Result.Failure("Every status must be mapped before records can be moved.");
        }

        var target = remap.For(StatusId);
        if (target is null)
        {
            // Either the record is already on the target workflow, or it holds a status from neither —
            // both mean this remap cannot speak for it, and guessing would strand it silently.
            return remap.ToWorkflowId == StatusWorkflowId
                ? Result.Success()
                : Result.Failure("This record's status is not in the workflow being moved from.");
        }

        // Exempt from the target workflow's transitions and their conditions: a migration must move every
        // record, whatever its status, and the remap has already decided where each one lands.
        return MoveOntoWorkflow(target, actor, timestamp, reason);
    }

    /// <summary>
    /// Moves this one record onto another workflow, into a status chosen there, recording the switch.
    /// </summary>
    /// <remarks>
    /// For an aggregate whose records can change container one at a time — an intake request moved to a desk
    /// that follows a different workflow — where the status it lands in is decided for that record rather
    /// than by a remap of the whole scope. Like <see cref="SwitchWorkflow"/>, the move is exempt from the
    /// target workflow's transitions and raises <see cref="StatusWorkflowSwitchedEvent"/>.
    /// </remarks>
    /// <param name="target">A status of a workflow other than the record's own.</param>
    protected Result SwitchWorkflowTo(StatusRef target, EventActor actor, Instant timestamp, string? reason = null)
    {
        Guard.Against.Null(target, nameof(target));
        Guard.Against.Null(actor, nameof(actor));

        if (target.WorkflowId == StatusWorkflowId)
        {
            throw new ArgumentException("The status belongs to the record's own workflow; apply it rather than switching.", nameof(target));
        }

        return MoveOntoWorkflow(target, actor, timestamp, reason);
    }

    /// <summary>
    /// Moves this record onto the workflow its new record type follows, recording the switch.
    /// </summary>
    /// <remarks>
    /// For an aggregate whose records change record type: within one scope, each type follows its own
    /// workflow, so a Story becoming a Bug may change workflow. Without a choice, the record lands by
    /// <see cref="StatusWorkflow.LandingStatusFor"/> — the same alias, else the first status of the same
    /// category — and when the target has neither, the caller must choose. A target that is the record's
    /// own workflow changes nothing.
    /// </remarks>
    /// <param name="target">The workflow the record's new type follows in its scope.</param>
    /// <param name="chosenStatusId">A status of <paramref name="target"/> chosen for this record, or null to land by the rule.</param>
    protected Result SwitchRecordTypeWorkflow(
        StatusWorkflow target, Guid? chosenStatusId, EventActor actor, Instant timestamp, string? reason = null)
    {
        Guard.Against.Null(target, nameof(target));

        if (target.Id == StatusWorkflowId)
        {
            return Result.Success();
        }

        var landingId = chosenStatusId ?? target.LandingStatusFor(StatusAliasValue, StatusCategory)?.Id;
        if (landingId is null)
        {
            return Result.Failure(
                $"'{target.Name}' has no status matching '{StatusName}'. Choose the status this record moves to.");
        }

        var landing = target.TargetRef(landingId.Value);

        return landing is null
            ? Result.Failure($"The chosen status does not belong to '{target.Name}'.")
            : SwitchWorkflowTo(landing, actor, timestamp, reason);
    }

    private Result MoveOntoWorkflow(StatusRef target, EventActor actor, Instant timestamp, string? reason)
    {
        var from = new StatusRef(StatusWorkflowId, StatusId, StatusName, StatusCategory, StatusAliasValue);

        var applied = ApplyStatus(target, actor, timestamp, reason, enforceTransitions: false);
        if (applied.IsFailure)
        {
            return applied;
        }

        if (from.StatusId != target.StatusId)
        {
            var transition = _statusTransitions[^1];

            AddDomainEvent(new StatusWorkflowSwitchedEvent(
                transition.Id,
                StatusOwnerType,
                Id,
                from.WorkflowId, from.StatusId, from.Category, from.Alias,
                target.WorkflowId, target.StatusId, target.Category, target.Alias,
                transition.Reason,
                actor,
                timestamp));
        }

        return Result.Success();
    }
}
