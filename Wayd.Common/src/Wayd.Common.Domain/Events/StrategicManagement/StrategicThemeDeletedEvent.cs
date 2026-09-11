using System.Text.Json.Serialization;
using Wayd.Common.Domain.Events;
using NodaTime;

namespace Wayd.Common.Domain.Events.StrategicManagement;

public sealed record StrategicThemeDeletedEvent : DomainEvent, IAggregateEvent
{
    public StrategicThemeDeletedEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicTheme";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
