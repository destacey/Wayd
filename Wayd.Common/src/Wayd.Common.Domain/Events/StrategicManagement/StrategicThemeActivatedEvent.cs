using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StrategicManagement;

/// <summary>
/// A proposed strategic theme was activated.
/// </summary>
/// <remarks>
/// Only a proposed theme can be activated, so the type records both ends of the transition.
/// </remarks>
public sealed record StrategicThemeActivatedEvent : DomainEvent, IAggregateEvent
{
    public StrategicThemeActivatedEvent(Guid id, EventActor actor, Instant timestamp)
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
