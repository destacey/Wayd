using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// An OIDC provider was deleted. Carries its name because the provider is gone by the time anyone reads the
/// entry.
/// </summary>
public sealed record OidcProviderDeletedEvent : DomainEvent<OidcProviderDeletedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    [JsonConstructor]
    public OidcProviderDeletedEvent(Guid id, string name, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "OidcProvider";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
