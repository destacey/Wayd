using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// A connection was tied to the outside system it reaches, once it learned the system's id. A connection is
/// linked once and never moves to another system.
/// </summary>
public sealed record ConnectionExternalSystemLinkedEvent : DomainEvent<ConnectionExternalSystemLinkedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ConnectionExternalSystemLinkedEvent(Guid id, Guid externalSystemId, Connector connector, string systemId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        ExternalSystemId = externalSystemId;
        Connector = connector;
        SystemId = systemId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public Guid ExternalSystemId { get; }
    public Connector Connector { get; }

    /// <summary>The system's own identifier for itself.</summary>
    public string SystemId { get; }

    [JsonIgnore]
    public string AggregateType => "Connection";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
