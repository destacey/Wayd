using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// One thing an accepted intake request became: a project or work item that delivers it.
/// </summary>
/// <remarks>
/// The link lives here and only here: neither PPM nor Work stores anything about requests, so who may see that
/// a project or work item came from a request is decided by the request's desk, like any other read of it. A
/// request points at a target at most once. The outcome outlives its target: a deleted project's or work
/// item's outcome stays as the record of what was decided.
/// </remarks>
public sealed class IntakeRequestOutcome
{
    private readonly List<IntakeRequestOutcomeDeliverable> _deliverables = [];

    private IntakeRequestOutcome() { }

    internal IntakeRequestOutcome(Guid requestId, IntakeRequestOutcomeKind kind, Guid targetId, string? note,
        IEnumerable<Guid> deliverableIds, Guid addedById, Instant addedOn)
    {
        RequestId = requestId;
        Kind = kind;
        TargetId = targetId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        AddedById = addedById;
        AddedOn = addedOn;
        _deliverables.AddRange(deliverableIds.Distinct().Select(id => new IntakeRequestOutcomeDeliverable(requestId, targetId, id)));
    }

    /// <summary>The request the outcome belongs to.</summary>
    public Guid RequestId { get; private init; }

    /// <summary>What the request became.</summary>
    public IntakeRequestOutcomeKind Kind { get; private init; }

    /// <summary>
    /// The project or work item the outcome points at, by its id in its own module; <see cref="Kind"/> says
    /// which.
    /// </summary>
    public Guid TargetId { get; private init; }

    /// <summary>
    /// What the outcome covers, in the agent's words. Always present for existing project scope, where it
    /// says what the project absorbed, since the project's own definition is not changed.
    /// </summary>
    public string? Note { get; private init; }

    /// <summary>The employee who recorded the outcome.</summary>
    public Guid AddedById { get; private init; }

    /// <summary>When the outcome was recorded.</summary>
    public Instant AddedOn { get; private init; }

    /// <summary>
    /// For existing project scope, the project's deliverables that meet the request; empty for the other kinds.
    /// Loaded with the outcomes whenever they are, since changing them reads what is already mapped.
    /// </summary>
    public IReadOnlyCollection<IntakeRequestOutcomeDeliverable> Deliverables => _deliverables.AsReadOnly();

    /// <summary>The ids of <see cref="Deliverables"/>.</summary>
    public Guid[] DeliverableIds() => [.. _deliverables.Select(d => d.DeliverableId)];

    /// <summary>
    /// Replaces the mapped deliverables with <paramref name="deliverableIds"/>, keeping the rows that stay.
    /// </summary>
    /// <returns>What the change added and removed; both empty when nothing changed.</returns>
    internal (Guid[] Added, Guid[] Removed) SetDeliverables(IEnumerable<Guid> deliverableIds)
    {
        var target = deliverableIds.ToHashSet();

        var removed = _deliverables.Where(d => !target.Contains(d.DeliverableId)).ToList();
        foreach (var row in removed)
            _deliverables.Remove(row);

        Guid[] added = [.. target.Where(id => _deliverables.All(d => d.DeliverableId != id))];
        _deliverables.AddRange(added.Select(id => new IntakeRequestOutcomeDeliverable(RequestId, TargetId, id)));

        return (added, [.. removed.Select(r => r.DeliverableId)]);
    }

    /// <summary>
    /// Whether the deliverables an outcome is mapped to meet the request: true when every one still on the
    /// project is Accepted, false while any is not, and null when none is mapped or still on the project.
    /// </summary>
    /// <remarks>
    /// Derived when read, never stored, from Demand Management's copies of the mapped deliverables. A descoped
    /// deliverable keeps the outcome from being met, since what the request needed was dropped; one deleted from
    /// the project has no copy, and never counted as a commitment.
    /// </remarks>
    /// <param name="mapped">The copies of the mapped deliverables that still exist.</param>
    public static bool? IsMet(IEnumerable<DemandProjectDeliverable> mapped)
    {
        var copies = mapped.ToList();
        return copies.Count == 0 ? null : copies.All(d => d.IsAccepted);
    }
}
