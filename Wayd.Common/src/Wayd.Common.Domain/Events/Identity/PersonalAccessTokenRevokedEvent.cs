using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A personal access token was revoked and can no longer authenticate. The actor is who revoked it. Also
/// listed on the owning user's Activity.
/// </summary>
public sealed record PersonalAccessTokenRevokedEvent : DomainEvent<PersonalAccessTokenRevokedEvent>, IDomainEventDescriptor, IRelatedAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    [JsonConstructor]
    public PersonalAccessTokenRevokedEvent(Guid id, string userId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        UserId = userId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>The account that owns the token.</summary>
    public string UserId { get; }

    [JsonIgnore]
    public string AggregateType => "PersonalAccessToken";
    [JsonIgnore]
    public Guid AggregateId => Id;
    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new("ApplicationUser", Guid.Parse(UserId))];
}
