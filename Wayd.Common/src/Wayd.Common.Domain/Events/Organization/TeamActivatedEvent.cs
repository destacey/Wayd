using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

public sealed record TeamActivatedEvent : DomainEvent, IAggregateEvent
{
    public TeamActivatedEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Timestamp = timestamp;
    }

    public Guid Id { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
