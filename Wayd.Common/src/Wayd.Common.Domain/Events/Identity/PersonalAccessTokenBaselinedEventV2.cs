using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// Tracking began for a personal access token that existed before <see cref="PersonalAccessTokenCreatedEventV2"/>
/// was recorded. Carries that event's payload, describing the token as it stood at <see cref="DomainEvent.Timestamp"/>.
/// Also listed on the owning user's Activity, as the creation event is.
/// </summary>
public sealed record PersonalAccessTokenBaselinedEventV2 : BaselineEvent<PersonalAccessTokenBaselinedEventV2, PersonalAccessTokenCreatedEventV2>, IRelatedAggregateEvent
{
    public PersonalAccessTokenBaselinedEventV2(Guid id, string userId, string name, Instant expiresAt, TokenScope scope, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp)
        : base("PersonalAccessToken", id, recordCreatedOn, recordCreatedById, timestamp, "2.0")
    {
        Id = id;
        UserId = userId;
        Name = name;
        ExpiresAt = expiresAt;
        Scope = scope;
    }

    public Guid Id { get; }
    public string UserId { get; }
    public string Name { get; }
    public Instant ExpiresAt { get; }

    /// <summary>How much of its owner's access the token may use.</summary>
    public TokenScope Scope { get; }

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new("ApplicationUser", Guid.Parse(UserId))];
}
