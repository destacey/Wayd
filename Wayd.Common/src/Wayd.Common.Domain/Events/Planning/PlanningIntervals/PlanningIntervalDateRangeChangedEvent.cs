using System.Text.Json.Serialization;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervals;

/// <summary>
/// A planning interval's start or end moved.
/// </summary>
public sealed record PlanningIntervalDateRangeChangedEvent : DomainEvent<PlanningIntervalDateRangeChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    [JsonConstructor]
    public PlanningIntervalDateRangeChangedEvent(
        Guid id,
        int key,
        LocalDateRange previousDateRange,
        LocalDateRange dateRange,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousDateRange = previousDateRange;
        DateRange = dateRange;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public LocalDateRange PreviousDateRange { get; }
    public LocalDateRange DateRange { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningInterval";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
