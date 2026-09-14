using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervals;

/// <summary>
/// Teams were added to or removed from a planning interval.
/// </summary>
/// <remarks>
/// A removed team's sprint mappings go with it, recorded by the <see cref="PlanningIntervalSprintMappingsChangedEvent"/>
/// raised alongside this one.
/// </remarks>
public sealed record PlanningIntervalTeamsChangedEvent : DomainEvent<PlanningIntervalTeamsChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public PlanningIntervalTeamsChangedEvent(
        Guid id,
        int key,
        Guid[] added,
        Guid[] removed,
        Guid[] teamIds,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Added = [.. added];
        Removed = [.. removed];
        TeamIds = [.. teamIds];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The teams that joined the interval.</summary>
    public Guid[] Added { get; }

    /// <summary>The teams that left the interval.</summary>
    public Guid[] Removed { get; }

    /// <summary>Every team in the interval after the change.</summary>
    public Guid[] TeamIds { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningInterval";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
