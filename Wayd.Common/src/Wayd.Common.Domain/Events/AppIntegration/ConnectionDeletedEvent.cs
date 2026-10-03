using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// A connection was deleted. Carries its name and connector because the connection is gone by the time
/// anyone reads the entry.
/// </summary>
public sealed record ConnectionDeletedEvent : DomainEvent<ConnectionDeletedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    [JsonConstructor]
    public ConnectionDeletedEvent(Guid id, string name, Connector connector, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;
        Connector = connector;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Name { get; }
    public Connector Connector { get; }

    [JsonIgnore]
    public string AggregateType => "Connection";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
