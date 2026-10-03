using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// A mapping seeded from an address took on the external system's real id for that user, the first time a sync
/// reported one.
/// </summary>
/// <remarks>
/// Carries neither id: the placeholder it replaced was the user's address, and the new id is read from the mapping.
/// </remarks>
public sealed record ExternalIdentityMappingRekeyedEvent : DomainEvent<ExternalIdentityMappingRekeyedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ExternalIdentityMappingRekeyedEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    [JsonIgnore]
    public string AggregateType => "ExternalIdentityMapping";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
