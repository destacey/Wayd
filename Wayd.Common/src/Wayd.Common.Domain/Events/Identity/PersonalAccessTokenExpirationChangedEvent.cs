using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A personal access token's expiration moved. Also listed on the owning user's Activity.
/// </summary>
public sealed record PersonalAccessTokenExpirationChangedEvent : DomainEvent<PersonalAccessTokenExpirationChangedEvent>, IDomainEventDescriptor, IRelatedAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    [JsonConstructor]
    public PersonalAccessTokenExpirationChangedEvent(Guid id, string userId, Instant previousExpiresAt, Instant expiresAt, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        UserId = userId;
        PreviousExpiresAt = previousExpiresAt;
        ExpiresAt = expiresAt;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>The account that owns the token.</summary>
    public string UserId { get; }

    public Instant PreviousExpiresAt { get; }
    public Instant ExpiresAt { get; }

    [JsonIgnore]
    public string AggregateType => "PersonalAccessToken";
    [JsonIgnore]
    public Guid AggregateId => Id;
    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new("ApplicationUser", Guid.Parse(UserId))];
}
