using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// The external system started reporting a different address, display name or handle for the user.
/// </summary>
/// <remarks>
/// Carries no values, before or after, for the reason <see cref="ExternalIdentityMappingCreatedEvent"/> carries none.
/// </remarks>
public sealed record ExternalIdentityMappingProfileChangedEvent : DomainEvent<ExternalIdentityMappingProfileChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ExternalIdentityMappingProfileChangedEvent(Guid id, EventActor actor, Instant timestamp)
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
