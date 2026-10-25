using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A user created a personal access token.
/// </summary>
/// <remarks>
/// Carries nothing derived from the token itself — neither its hash nor its identifier, which is a prefix of
/// the plain text. Also listed on the owning user's Activity.
/// </remarks>
public sealed record PersonalAccessTokenCreatedEventV2 : DomainEvent<PersonalAccessTokenCreatedEventV2>, IDomainEventDescriptor, IRelatedAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public PersonalAccessTokenCreatedEventV2(Guid id, string userId, string name, Instant expiresAt, TokenScope scope, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        UserId = userId;
        Name = name;
        ExpiresAt = expiresAt;
        Scope = scope;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>The account that owns the token.</summary>
    public string UserId { get; }

    public string Name { get; }
    public Instant ExpiresAt { get; }

    /// <summary>How much of its owner's access the token may use.</summary>
    public TokenScope Scope { get; }

    [JsonIgnore]
    public string AggregateType => "PersonalAccessToken";
    [JsonIgnore]
    public Guid AggregateId => Id;
    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new("ApplicationUser", Guid.Parse(UserId))];
}
