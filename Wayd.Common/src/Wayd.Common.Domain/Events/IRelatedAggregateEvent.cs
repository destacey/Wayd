namespace Wayd.Common.Domain.Events;

/// <summary>
/// Implemented by domain events that concern records besides the aggregate they are raised on, so the
/// activity log lists the one entry on each of those records as well.
/// </summary>
/// <remarks>
/// <para>
/// One event filed under several records, never a second event raised for the other side. A product moving
/// between parents is one fact whichever parent is reading about it, and a mirror event would reach every
/// consumer twice.
/// </para>
/// <para>
/// Implement it <c>[JsonIgnore]</c>, computed from ids the payload already carries. Kept out of the payload,
/// adopting it changes no published shape and needs no version bump; computed from the payload, entries
/// written before an event adopted it can be backfilled from what they recorded.
/// </para>
/// </remarks>
public interface IRelatedAggregateEvent : IAggregateEvent
{
    /// <summary>
    /// The other records this event concerns. Entries naming the event's own aggregate, or naming a record
    /// twice, are dropped when the activity entry is written.
    /// </summary>
    IReadOnlyCollection<AggregateReference> RelatedAggregates { get; }
}

/// <summary>
/// A record an activity entry concerns, by the same type name and id the log files entries under.
/// </summary>
public sealed record AggregateReference(string AggregateType, Guid AggregateId);
