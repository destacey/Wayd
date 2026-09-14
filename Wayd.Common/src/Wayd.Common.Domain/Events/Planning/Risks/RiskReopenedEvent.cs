using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Risks;

/// <summary>
/// A closed risk was reopened, clearing its closed date.
/// </summary>
public sealed record RiskReopenedEvent : DomainEvent<RiskReopenedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    [JsonConstructor]
    public RiskReopenedEvent(Guid id, int key, Instant? previousClosedDate, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousClosedDate = previousClosedDate;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>When the risk had been closed. Null for a risk imported closed without a closed date.</summary>
    public Instant? PreviousClosedDate { get; }

    [JsonIgnore]
    public string AggregateType => "Risk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
