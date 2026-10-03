using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// Tracking began for a personal access token that existed before <see cref="PersonalAccessTokenCreatedEvent"/>
/// was recorded. Carries that event's payload, describing the token as it stood at <see cref="DomainEvent.Timestamp"/>.
/// Also listed on the owning user's Activity, as the creation event is.
/// </summary>
public sealed record PersonalAccessTokenBaselinedEvent : BaselineEvent<PersonalAccessTokenBaselinedEvent, PersonalAccessTokenCreatedEvent>, IRelatedAggregateEvent
{
    public PersonalAccessTokenBaselinedEvent(Guid id, string userId, string name, Instant expiresAt, string? scopes, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp)
        : base("PersonalAccessToken", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
    {
        Id = id;
        UserId = userId;
        Name = name;
        ExpiresAt = expiresAt;
        Scopes = scopes;
    }

    public Guid Id { get; }
    public string UserId { get; }
    public string Name { get; }
    public Instant ExpiresAt { get; }
    public string? Scopes { get; }

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new("ApplicationUser", Guid.Parse(UserId))];
}
