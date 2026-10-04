using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// Tracking began for a roadmap that existed before <see cref="RoadmapCreatedEvent"/> was recorded. Carries that
/// event's payload, describing the roadmap as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record RoadmapBaselinedEvent : BaselineEvent<RoadmapBaselinedEvent, RoadmapCreatedEvent>
{
    public RoadmapBaselinedEvent(
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
        Instant? recordCreatedOn,
        Guid? recordCreatedById,
        Instant timestamp)
        : base("Roadmap", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
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
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }
    public LocalDateRange DateRange { get; }
    public Visibility Visibility { get; }
    public RoadmapState State { get; }
    public Guid[] ManagerIds { get; }
    public RoadmapColorValues[] Colors { get; }
    public RoadmapItemValues[] Items { get; }
}
