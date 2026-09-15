using System.Text.Json.Serialization;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervals;

/// <summary>
/// An iteration of a planning interval moved its start or end.
/// </summary>
public sealed record PlanningIntervalIterationDateRangeChangedEvent : DomainEvent<PlanningIntervalIterationDateRangeChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    [JsonConstructor]
    public PlanningIntervalIterationDateRangeChangedEvent(
        Guid id,
        int key,
        Guid iterationId,
        LocalDateRange previousDateRange,
        LocalDateRange dateRange,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        IterationId = iterationId;
        PreviousDateRange = previousDateRange;
        DateRange = dateRange;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid IterationId { get; }
    public LocalDateRange PreviousDateRange { get; }
    public LocalDateRange DateRange { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningInterval";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
