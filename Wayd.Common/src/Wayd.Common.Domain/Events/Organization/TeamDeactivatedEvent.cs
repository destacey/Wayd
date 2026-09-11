using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

public sealed record TeamDeactivatedEvent : DomainEvent, IAggregateEvent
{
    public TeamDeactivatedEvent(Guid id, LocalDate inactiveDate, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        InactiveDate = inactiveDate;
        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public LocalDate InactiveDate { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
