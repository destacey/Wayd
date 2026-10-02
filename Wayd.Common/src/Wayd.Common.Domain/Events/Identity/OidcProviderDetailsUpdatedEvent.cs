using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// The label an OIDC provider shows on the login page was edited.
/// </summary>
public sealed record OidcProviderDetailsUpdatedEvent : DomainEvent<OidcProviderDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public OidcProviderDetailsUpdatedEvent(Guid id, string label, OidcProviderDetails previous, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Label = label;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Label { get; }

    /// <summary>The details this edit replaced.</summary>
    public OidcProviderDetails Previous { get; }

    [JsonIgnore]
    public string AggregateType => "OidcProvider";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
