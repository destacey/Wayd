using NodaTime;
using Wayd.Common.Domain.Identity;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// Tracking began for an OIDC provider that existed before <see cref="OidcProviderCreatedEvent"/> was recorded.
/// Carries that event's payload, describing the provider as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record OidcProviderBaselinedEvent : BaselineEvent<OidcProviderBaselinedEvent, OidcProviderCreatedEvent>
{
    public OidcProviderBaselinedEvent(
        Guid id,
        string name,
        string label,
        OidcProviderType providerType,
        OidcProviderConfiguration configuration,
        OidcProviderRegistrationPolicy registrationPolicy,
        bool isEnabled,
        Instant? recordCreatedOn,
        Guid? recordCreatedById,
        Instant timestamp)
        : base("OidcProvider", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
    {
        Id = id;
        Name = name;
        Label = label;
        ProviderType = providerType;
        Configuration = configuration;
        RegistrationPolicy = registrationPolicy;
        IsEnabled = isEnabled;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string Label { get; }
    public OidcProviderType ProviderType { get; }
    public OidcProviderConfiguration Configuration { get; }
    public OidcProviderRegistrationPolicy RegistrationPolicy { get; }
    public bool IsEnabled { get; }
}
