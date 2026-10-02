using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// How Wayd validates an OIDC provider's tokens changed: its authority, client, audience, scopes, tenant
/// allowlist or clock skew.
/// </summary>
public sealed record OidcProviderConfigurationChangedEvent : DomainEvent<OidcProviderConfigurationChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public OidcProviderConfigurationChangedEvent(Guid id, OidcProviderConfiguration configuration, OidcProviderConfiguration previous, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Configuration = configuration;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public OidcProviderConfiguration Configuration { get; }

    /// <summary>The configuration this change replaced.</summary>
    public OidcProviderConfiguration Previous { get; }

    [JsonIgnore]
    public string AggregateType => "OidcProvider";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
