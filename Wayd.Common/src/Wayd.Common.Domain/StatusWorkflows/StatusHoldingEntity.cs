using Ardalis.GuardClauses;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// An aggregate that holds a status from a <see cref="StatusWorkflow"/>: which status, which workflow it
/// came from, and what the status was called and meant when applied.
/// </summary>
/// <remarks>
/// Every status-holding record has this state; who decides its moves does not. The constructor is
/// <c>private protected</c>, so an aggregate derives from a kind in this assembly that decides how its
/// status moves — <see cref="StatusTrackedEntity"/>, which checks each move against the workflow's
/// transitions and records it as a <see cref="StatusTransition"/> — never from this directly.
/// <para>
/// A record does not store which workflow it should follow. Its container and its record type decide,
/// through the container's <c>WorkflowAssignment</c> and the scheme it points at — records of one type
/// in one container never diverge, so a choice per record would only be a value that can drift.
/// </para>
/// </remarks>
public abstract class StatusHoldingEntity : BaseAuditableEntity
{
    private protected StatusHoldingEntity() { }

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
    /// It is also what makes a batched workflow migration resumable: a re-run tells a record already
    /// moved from one holding a status of neither workflow by this value. Transition history cannot
    /// answer it — it is not a navigation, and <c>DrainStatusTransitions</c> empties it on every save.
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

    /// <summary>Whether the record has been given a status yet.</summary>
    private protected bool HasStatus => StatusId != Guid.Empty;

    /// <summary>The status the record holds now, as a reference, or <c>null</c> before its first.</summary>
    private protected StatusRef? CurrentStatus =>
        HasStatus ? new StatusRef(StatusWorkflowId, StatusId, StatusName, StatusCategory, StatusAliasValue) : null;

    /// <summary>
    /// Stores <paramref name="status"/> as the record's status, with no check and no record of the move.
    /// </summary>
    /// <remarks>
    /// <c>private protected</c> so only the kinds in this assembly reach it: an aggregate elsewhere
    /// cannot set its status without going through the rules of the kind it derives from.
    /// </remarks>
    private protected void SetStatus(StatusRef status)
    {
        StatusId = status.StatusId;
        StatusWorkflowId = status.WorkflowId;
        StatusName = status.Name;
        StatusCategory = status.Category;
        StatusAliasValue = status.Alias;
    }
}
