using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// An OIDC provider was enabled, so it is offered on the login page and accepts sign-ins.
/// </summary>
public sealed record OidcProviderEnabledEvent : DomainEvent<OidcProviderEnabledEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    [JsonConstructor]
    public OidcProviderEnabledEvent(Guid id, EventActor actor, Instant timestamp)
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
