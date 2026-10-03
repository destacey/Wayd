using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// An OIDC provider was disabled. It is hidden from the login page and rejects sign-ins, keeping its
/// configuration and the identities that point at it.
/// </summary>
public sealed record OidcProviderDisabledEvent : DomainEvent<OidcProviderDisabledEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    [JsonConstructor]
    public OidcProviderDisabledEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    [JsonIgnore]
    public string AggregateType => "OidcProvider";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
