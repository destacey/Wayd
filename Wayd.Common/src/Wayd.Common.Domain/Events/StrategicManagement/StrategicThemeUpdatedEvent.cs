using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Interfaces.StrategicManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.StrategicManagement;

public sealed record StrategicThemeUpdatedEvent : DomainEvent
{
    public StrategicThemeUpdatedEvent(IStrategicThemeData strategicTheme, Instant timestamp)
        : this(strategicTheme.Id, strategicTheme.Name, strategicTheme.Description, strategicTheme.State, timestamp)
    {
    }

    // Deserialization constructor for the Wolverine durable outbox (STJ binds parameters to properties by
    // name; the primary constructor's `strategicTheme` parameter cannot be bound).
    [JsonConstructor]
    public StrategicThemeUpdatedEvent(Guid id, string name, string description, StrategicThemeState state, Instant timestamp)
    {
        Id = id;
        Name = name;
        Description = description;
        State = state;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string Description { get; }
    public StrategicThemeState State { get; }
}
