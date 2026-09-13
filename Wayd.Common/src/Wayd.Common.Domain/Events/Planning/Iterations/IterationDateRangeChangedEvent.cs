using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// The iteration's start or end moved, was set, or was cleared.
/// </summary>
public sealed record IterationDateRangeChangedEvent : DomainEvent, IAggregateEvent
{
    public IterationDateRangeChangedEvent(Guid id, int key, IterationDateRange previousDateRange, IterationDateRange dateRange, EventActor actor, Instant timestamp)
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
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
