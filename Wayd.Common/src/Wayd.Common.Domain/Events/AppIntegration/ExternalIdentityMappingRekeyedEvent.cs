using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// A mapping seeded from an address took on the external system's real id for that user, the first time a sync
/// reported one.
/// </summary>
/// <remarks>
/// Only the new id: the placeholder it replaced was the user's address.
/// </remarks>
public sealed record ExternalIdentityMappingRekeyedEvent : DomainEvent<ExternalIdentityMappingRekeyedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ExternalIdentityMappingRekeyedEvent(Guid id, string externalId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        ExternalId = externalId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string ExternalId { get; }

    [JsonIgnore]
    public string AggregateType => "ExternalIdentityMapping";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
