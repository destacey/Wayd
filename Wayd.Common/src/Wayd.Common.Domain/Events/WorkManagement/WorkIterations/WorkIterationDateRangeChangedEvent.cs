using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.WorkManagement.WorkIterations;

/// <summary>
/// The Work copy of an iteration's start or end moved, was set, or was cleared.
/// </summary>
public sealed record WorkIterationDateRangeChangedEvent : DomainEvent<WorkIterationDateRangeChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    public WorkIterationDateRangeChangedEvent(Guid id, int key, IterationDateRange previousDateRange, IterationDateRange dateRange, EventActor actor, Instant timestamp)
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
    public IterationDateRange PreviousDateRange { get; }
    public IterationDateRange DateRange { get; }

    [JsonIgnore]
    public string AggregateType => "WorkIteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
