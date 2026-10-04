using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// A roadmap's configured colors were changed.
/// </summary>
/// <remarks>
/// Colors are edited as a set and have no identity beyond their hex value, so the change carries the set before
/// and after rather than individual additions and removals.
/// </remarks>
public sealed record RoadmapColorsChangedEvent : DomainEvent<RoadmapColorsChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RoadmapColorsChangedEvent(
        Guid id,
        int key,
        RoadmapColorValues[] previousColors,
        RoadmapColorValues[] colors,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousColors = [.. previousColors];
        Colors = [.. colors];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The colors before the change, in display order.</summary>
    public RoadmapColorValues[] PreviousColors { get; }

    /// <summary>The colors after the change, in display order.</summary>
    public RoadmapColorValues[] Colors { get; }

    [JsonIgnore]
    public string AggregateType => "Roadmap";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
