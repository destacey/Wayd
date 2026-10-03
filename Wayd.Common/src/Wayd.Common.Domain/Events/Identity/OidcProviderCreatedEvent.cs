using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Identity;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// An administrator added an OIDC identity provider users can sign in with.
/// </summary>
public sealed record OidcProviderCreatedEvent : DomainEvent<OidcProviderCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public OidcProviderCreatedEvent(
        Guid id,
        string name,
        string label,
        OidcProviderType providerType,
        OidcProviderConfiguration configuration,
        OidcProviderRegistrationPolicy registrationPolicy,
        bool isEnabled,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;
        Label = label;
        ProviderType = providerType;
        Configuration = configuration;
        RegistrationPolicy = registrationPolicy;
        IsEnabled = isEnabled;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>The provider's stable key, written on every identity that signs in through it.</summary>
    public string Name { get; }

    /// <summary>What the login page shows for the provider.</summary>
    public string Label { get; }

    public OidcProviderType ProviderType { get; }
    public OidcProviderConfiguration Configuration { get; }
    public OidcProviderRegistrationPolicy RegistrationPolicy { get; }
    public bool IsEnabled { get; }

    [JsonIgnore]
    public string AggregateType => "OidcProvider";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
