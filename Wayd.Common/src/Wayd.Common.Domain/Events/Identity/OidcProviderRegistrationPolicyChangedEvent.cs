using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// Whether first-time sign-ins through an OIDC provider create an account, and on what terms, changed.
/// </summary>
public sealed record OidcProviderRegistrationPolicyChangedEvent : DomainEvent<OidcProviderRegistrationPolicyChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public OidcProviderRegistrationPolicyChangedEvent(Guid id, OidcProviderRegistrationPolicy registrationPolicy, OidcProviderRegistrationPolicy previous, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        RegistrationPolicy = registrationPolicy;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public OidcProviderRegistrationPolicy RegistrationPolicy { get; }

    /// <summary>The policy this change replaced.</summary>
    public OidcProviderRegistrationPolicy Previous { get; }

    [JsonIgnore]
    public string AggregateType => "OidcProvider";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
