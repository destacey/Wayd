using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.WorkManagement.WorkIterations;

/// <summary>
/// The Work copy of an iteration's start or end moved, was set, or was cleared.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="WorkIterationDateRangeChangedEventV2"/> replaced
/// it. Kept so every payload written as this type still deserializes into it — its name and members are the
/// contract those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by WorkIterationDateRangeChangedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record WorkIterationDateRangeChangedEvent : DomainEvent<WorkIterationDateRangeChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    public WorkIterationDateRangeChangedEvent(Guid id, int key, IterationDateRangeV1 previousDateRange, IterationDateRangeV1 dateRange, EventActor actor, Instant timestamp)
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
    public IterationDateRangeV1 PreviousDateRange { get; }
    public IterationDateRangeV1 DateRange { get; }

    [JsonIgnore]
    public string AggregateType => "WorkIteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
