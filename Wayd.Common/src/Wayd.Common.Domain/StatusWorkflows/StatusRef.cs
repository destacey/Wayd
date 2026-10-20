using Ardalis.GuardClauses;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// A status already resolved from its workflow, ready to be stored on a record: which workflow it came
/// from, the id to keep, the name and category to freeze alongside it, and the alias it carries.
/// </summary>
/// <remarks>
/// <para>
/// This type exists so an aggregate can hold a configurable status without depending on the workflow
/// engine at write time. The application layer loads the workflow, resolves the target status, and
/// hands the aggregate this; the aggregate stores what it needs and never consults the workflow again.
/// </para>
/// <para>
/// Denormalizing the category is deliberate. The alternative — resolving it through the workflow on
/// every read — makes every invariant and every projection depend on the workflow being loaded, which
/// is the same silent failure mode that makes PPM's ancestry authorization hard to work in: a missing
/// include produces a wrong answer rather than an error.
/// </para>
/// <para>
/// <see cref="Name"/> travels with the reference because a status can be renamed while its workflow is
/// active. Records and transitions freeze it, so a rename cannot rewrite what past rows read as.
/// </para>
/// <para>
/// A record type, but never compared: its equality would compare the transition sets and conditions it
/// carries by reference. Compare <see cref="StatusId"/> instead.
/// </para>
/// </remarks>
public sealed record StatusRef
{
    public StatusRef(
        Guid workflowId,
        Guid statusId,
        string name,
        StatusCategory category,
        int alias = StatusWorkflow.NoAlias,
        IReadOnlySet<Guid>? allowedFrom = null,
        IReadOnlyDictionary<Guid, IReadOnlySet<TransitionConditionKind>>? conditionsFrom = null,
        IReadOnlySet<TransitionConditionKind>? metConditions = null)
    {
        WorkflowId = Guard.Against.Default(workflowId, nameof(workflowId));
        StatusId = Guard.Against.Default(statusId, nameof(statusId));
        Name = Guard.Against.NullOrWhiteSpace(name, nameof(name)).Trim();
        Category = category;
        Alias = alias;
        AllowedFrom = allowedFrom;
        ConditionsFrom = conditionsFrom ?? new Dictionary<Guid, IReadOnlySet<TransitionConditionKind>>();
        MetConditions = metConditions ?? new HashSet<TransitionConditionKind>();
    }

    /// <summary>
    /// The workflow this status came from, so a record can say which workflow governed it rather than
    /// leaving it to be inferred from a status row that may later be deleted.
    /// </summary>
    public Guid WorkflowId { get; }

    /// <summary>The status the record holds. Stable across renames.</summary>
    public Guid StatusId { get; }

    /// <summary>What the status was called when it was resolved. Frozen by whoever stores it.</summary>
    public string Name { get; }

    /// <summary>The status's bucket, stored alongside the id so reads never need the workflow.</summary>
    public StatusCategory Category { get; }

    /// <summary>
    /// The well-known meaning the status carries, or <see cref="StatusWorkflow.NoAlias"/>. An
    /// <c>int</c> because the meanings belong to the consuming module rather than to the engine — the
    /// caller casts to its own alias enum.
    /// </summary>
    public int Alias { get; }

    /// <summary>
    /// The statuses a record may move into this one from, or <c>null</c> when any move is allowed.
    /// </summary>
    /// <remarks>
    /// Set by the workflow that resolved the target (<see cref="StatusWorkflow.TargetRef"/>), so the
    /// record can refuse a move its workflow does not allow without loading the workflow — the same
    /// reason the category and alias travel here. Never stored: it describes the workflow at the moment
    /// of the move, not the record.
    /// </remarks>
    public IReadOnlySet<Guid>? AllowedFrom { get; init; }

    /// <summary>
    /// What a move into this status needs, by the status it moves from. A status with no entry, or one
    /// not in <see cref="AllowedFrom"/>, needs nothing beyond being allowed.
    /// </summary>
    /// <remarks>
    /// Set by the workflow alongside <see cref="AllowedFrom"/>, and never stored, for the same reasons.
    /// The record checks the conditions it can (<see cref="TransitionConditionKinds.IsSelfChecked"/>)
    /// against what the move carries, and the rest against <see cref="MetConditions"/>.
    /// </remarks>
    public IReadOnlyDictionary<Guid, IReadOnlySet<TransitionConditionKind>> ConditionsFrom { get; }

    /// <summary>
    /// The conditions the application layer has evaluated as met for this move — those a record cannot
    /// check for itself, such as a passed approval. Empty until <see cref="WithMetConditions"/>.
    /// </summary>
    public IReadOnlySet<TransitionConditionKind> MetConditions { get; }

    /// <summary>What a move into this status from <paramref name="fromStatusId"/> needs.</summary>
    public IReadOnlySet<TransitionConditionKind> ConditionsFor(Guid fromStatusId) =>
        ConditionsFrom.TryGetValue(fromStatusId, out var conditions) ? conditions : new HashSet<TransitionConditionKind>();

    /// <summary>
    /// A copy of this reference recording that <paramref name="met"/> were evaluated as met, for the
    /// conditions a record cannot check itself.
    /// </summary>
    /// <remarks>Built through the constructor rather than <c>with</c>, which an aggregate's history checks read as a state change.</remarks>
    public StatusRef WithMetConditions(IEnumerable<TransitionConditionKind> met)
    {
        Guard.Against.Null(met, nameof(met));

        return new StatusRef(
            WorkflowId, StatusId, Name, Category, Alias, AllowedFrom, ConditionsFrom, MetConditions.Union(met).ToHashSet());
    }

    /// <summary>
    /// Builds a reference from a workflow status, with no transition restriction.
    /// </summary>
    /// <remarks>
    /// For a record's initial status, which is exempt from the workflow's transitions. A move between
    /// statuses goes through <see cref="StatusWorkflow.TargetRef"/> so the workflow's restrictions travel
    /// with it.
    /// </remarks>
    public static StatusRef From(WorkflowStatus status)
    {
        Guard.Against.Null(status, nameof(status));

        return new StatusRef(status.WorkflowId, status.Id, status.Name, status.Category, status.Alias);
    }
}
