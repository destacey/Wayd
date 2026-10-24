using NodaTime;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows;

/// <summary>
/// A record entering a status: the part of a <see cref="StatusTransition"/> that time in status needs.
/// </summary>
/// <remarks>
/// A shape of its own rather than the entity, so a report can project only these columns across many
/// records instead of materializing whole transitions.
/// </remarks>
/// <param name="Sequence">The transition's per-record sequence — the only reliable order.</param>
/// <param name="WorkflowId">The workflow that governed the transition, which changes on a reassignment.</param>
/// <param name="StatusId">The status entered, as frozen on the transition.</param>
/// <param name="StatusName">What that status was called at the time.</param>
/// <param name="Category">The category it was in at the time.</param>
/// <param name="EnteredOn">When the record entered it.</param>
public sealed record StatusEntry(
    int Sequence,
    Guid WorkflowId,
    Guid StatusId,
    string StatusName,
    StatusCategory Category,
    Instant EnteredOn)
{
    /// <summary>The entry a stored transition records.</summary>
    public static StatusEntry From(StatusTransition transition) => new(
        transition.Sequence,
        transition.WorkflowId,
        transition.ToStatusId,
        transition.ToStatusName,
        transition.ToCategory,
        transition.ChangedOn);
}

/// <summary>
/// One stretch a record spent in one status, from entering it to leaving it.
/// </summary>
/// <param name="Sequence">The sequence of the transition that began the visit.</param>
/// <param name="LeftOn">When the record left the status, or <c>null</c> while it is still there.</param>
/// <param name="Duration">
/// The time spent, measured to now for a visit still open. Never negative: an entry recorded out of
/// order, or a clock behind the last entry, counts as no time rather than time taken back.
/// </param>
/// <param name="TouchesWorkflowSwitch">
/// Whether the visit began or ended with the record moving to another workflow. Such a visit's length
/// is real time in the record's history but not a measure of the status: a reassignment ends every
/// open stay at one instant, and the stay it begins started when the record entered its old status.
/// </param>
public sealed record StatusVisit(
    int Sequence,
    Guid StatusId,
    string StatusName,
    StatusCategory Category,
    Instant EnteredOn,
    Instant? LeftOn,
    Duration Duration,
    bool TouchesWorkflowSwitch)
{
    /// <summary>Whether this is the status the record holds now.</summary>
    public bool IsCurrent => LeftOn is null;
}

/// <summary>
/// The time a record spent in one status, across every visit to it.
/// </summary>
/// <param name="StatusName">
/// The name frozen on the latest visit, so a status renamed between visits reads as it was last called.
/// </param>
public sealed record StatusTimeTotal(
    Guid StatusId,
    string StatusName,
    StatusCategory Category,
    int Visits,
    Duration Total);

/// <summary>
/// How long a record has spent in each status, derived from its status history.
/// </summary>
/// <remarks>
/// <para>
/// Calculated, never stored: the transition log already holds every instant needed, and a stored
/// duration would be stale the moment it was written for the status the record is in now.
/// </para>
/// <para>
/// A visit ends at the record's next transition, whatever that transition was — including a remap onto
/// another workflow. Visits are keyed by the status id frozen on the transition, so history from an
/// earlier workflow keeps its own statuses rather than being folded into the new workflow's.
/// </para>
/// <para>
/// Calendar time throughout. Working-day durations need a working calendar the engine does not have.
/// </para>
/// </remarks>
public sealed record TimeInStatus(
    IReadOnlyList<StatusVisit> Visits,
    IReadOnlyList<StatusTimeTotal> ByStatus)
{
    /// <summary>
    /// Turns one record's status entries into visits and per-status totals.
    /// </summary>
    /// <param name="entries">One record's entries, in any order; they are ordered by sequence here.</param>
    /// <param name="now">The instant an open visit is measured to.</param>
    /// <returns>Visits oldest first; totals ordered by when each status was first entered.</returns>
    public static TimeInStatus Calculate(IEnumerable<StatusEntry> entries, Instant now)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var ordered = entries.OrderBy(e => e.Sequence).ToList();
        var visits = new List<StatusVisit>(ordered.Count);

        for (var i = 0; i < ordered.Count; i++)
        {
            var entry = ordered[i];
            var previous = i > 0 ? ordered[i - 1] : null;
            var next = i + 1 < ordered.Count ? ordered[i + 1] : null;

            var end = next?.EnteredOn ?? now;
            var duration = end > entry.EnteredOn ? end - entry.EnteredOn : Duration.Zero;
            var touchesSwitch = (previous is not null && previous.WorkflowId != entry.WorkflowId)
                || (next is not null && next.WorkflowId != entry.WorkflowId);

            visits.Add(new StatusVisit(
                entry.Sequence,
                entry.StatusId,
                entry.StatusName,
                entry.Category,
                entry.EnteredOn,
                next?.EnteredOn,
                duration,
                touchesSwitch));
        }

        var byStatus = visits
            .GroupBy(v => v.StatusId)
            .Select(g => new StatusTimeTotal(
                g.Key,
                g.Last().StatusName,
                g.Last().Category,
                g.Count(),
                g.Aggregate(Duration.Zero, (total, v) => total + v.Duration)))
            .ToList();

        return new TimeInStatus(visits, byStatus);
    }
}
