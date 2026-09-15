using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Risks;

/// <summary>
/// A risk was closed and is no longer being managed.
/// </summary>
public sealed record RiskClosedEvent : DomainEvent<RiskClosedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    [JsonConstructor]
    public RiskClosedEvent(Guid id, int key, Instant closedDate, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ClosedDate = closedDate;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Instant ClosedDate { get; }

    [JsonIgnore]
    public string AggregateType => "Risk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
