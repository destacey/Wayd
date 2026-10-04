using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// A roadmap was created, either from scratch or as a copy of another.
/// </summary>
/// <remarks>
/// A roadmap created from scratch has no items and no colors, which each have their own events. They are
/// carried because a copy starts with the items of the roadmap it was copied from, and because
/// <see cref="RoadmapBaselinedEvent"/> shares this shape and has to describe a roadmap that has both.
/// </remarks>
public sealed record RoadmapCreatedEvent : DomainEvent<RoadmapCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public RoadmapCreatedEvent(
        Guid id,
        int key,
        string name,
        string? description,
        LocalDateRange dateRange,
        Visibility visibility,
        RoadmapState state,
        Guid[] managerIds,
        RoadmapColorValues[] colors,
        RoadmapItemValues[] items,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        DateRange = dateRange;
        Visibility = visibility;
        State = state;
        ManagerIds = [.. managerIds];
        Colors = [.. colors];
        Items = [.. items];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }
    public LocalDateRange DateRange { get; }
    public Visibility Visibility { get; }
    public RoadmapState State { get; }

    /// <summary>The employees who manage the roadmap.</summary>
    public Guid[] ManagerIds { get; }

    /// <summary>The configured colors, in display order.</summary>
    public RoadmapColorValues[] Colors { get; }

    /// <summary>Every activity, milestone and timebox on the roadmap.</summary>
    public RoadmapItemValues[] Items { get; }

    [JsonIgnore]
    public string AggregateType => "Roadmap";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
