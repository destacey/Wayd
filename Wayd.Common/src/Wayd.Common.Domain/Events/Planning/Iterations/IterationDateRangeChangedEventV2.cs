using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// The iteration's planned start or end moved, was set, or was cleared.
/// </summary>
/// <remarks>
/// Supersedes <see cref="IterationDateRangeChangedEvent"/>, whose ranges carried instants. A new type rather
/// than a new version, because retyping a field breaks every consumer written against the old shape.
/// </remarks>
public sealed record IterationDateRangeChangedEventV2 : DomainEvent<IterationDateRangeChangedEventV2>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    public IterationDateRangeChangedEventV2(Guid id, int key, IterationDateRange previousDateRange, IterationDateRange dateRange, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
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
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
