using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StrategicManagement;

/// <summary>
/// A strategic theme's name or description was edited. Supersedes <see cref="StrategicThemeUpdatedEvent"/>.
/// </summary>
public sealed record StrategicThemeDetailsUpdatedEvent : DomainEvent, IAggregateEvent
{
    public StrategicThemeDetailsUpdatedEvent(Guid id, int key, string name, string description, StrategicThemeDetails? previous, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string Description { get; }

    /// <summary>
    /// The details this edit replaced. Grouped so that null can only mean "not recorded", as
    /// <c>ProjectDetailsUpdatedEvent.Previous</c> is.
    /// </summary>
    public StrategicThemeDetails? Previous { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicTheme";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
