using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervals;

/// <summary>
/// An iteration was added to a planning interval.
/// </summary>
public sealed record PlanningIntervalIterationAddedEvent : DomainEvent<PlanningIntervalIterationAddedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public PlanningIntervalIterationAddedEvent(
        Guid id,
        int key,
        Guid iterationId,
        string name,
        IterationCategory category,
        LocalDateRange dateRange,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        IterationId = iterationId;
        Name = name;
        Category = category;
        DateRange = dateRange;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid IterationId { get; }
    public string Name { get; }
    public IterationCategory Category { get; }
    public LocalDateRange DateRange { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningInterval";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
