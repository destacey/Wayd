using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// The iteration's start or end moved, was set, or was cleared.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="IterationDateRangeChangedEventV2"/> replaced it.
/// Kept so every payload written as this type still deserializes into it — its name and members are the
/// contract those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by IterationDateRangeChangedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record IterationDateRangeChangedEvent : DomainEvent<IterationDateRangeChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    public IterationDateRangeChangedEvent(Guid id, int key, IterationDateRangeV1 previousDateRange, IterationDateRangeV1 dateRange, EventActor actor, Instant timestamp)
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
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
