using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StrategicManagement;

/// <summary>
/// An active strategic theme was archived.
/// </summary>
/// <remarks>
/// Only an active theme can be archived, so the type records both ends of the transition.
/// </remarks>
public sealed record StrategicThemeArchivedEvent : DomainEvent, IAggregateEvent
{
    public StrategicThemeArchivedEvent(Guid id, EventActor actor, Instant timestamp)
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
