using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Interfaces.StrategicManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.StrategicManagement;

public sealed record StrategicThemeCreatedEvent : DomainEvent, IStrategicThemeData
{
    public StrategicThemeCreatedEvent(IStrategicThemeData strategicTheme, Instant timestamp)
        : this(strategicTheme.Id, strategicTheme.Key, strategicTheme.Name, strategicTheme.Description, strategicTheme.State, timestamp)
    {
    }

    // Deserialization constructor for the Wolverine durable outbox (STJ binds parameters to properties by
    // name; the primary constructor's `strategicTheme` parameter cannot be bound).
    [JsonConstructor]
    public StrategicThemeCreatedEvent(Guid id, int key, string name, string description, StrategicThemeState state, Instant timestamp)
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        State = state;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string Description { get; }
    public StrategicThemeState State { get; }
}
