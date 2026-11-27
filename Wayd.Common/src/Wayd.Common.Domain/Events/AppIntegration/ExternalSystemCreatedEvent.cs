using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// An outside system was recorded, the first time a connection learned its id.
/// </summary>
public sealed record ExternalSystemCreatedEvent : DomainEvent<ExternalSystemCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public ExternalSystemCreatedEvent(Guid id, Connector connector, string systemId, string name, string? baseUrl, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Connector = connector;
        SystemId = systemId;
        Name = name;
        BaseUrl = baseUrl;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public Connector Connector { get; }

    /// <summary>The system's own identifier for itself.</summary>
    public string SystemId { get; }
    public string Name { get; }
    public string? BaseUrl { get; }

    [JsonIgnore]
    public string AggregateType => "ExternalSystem";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
