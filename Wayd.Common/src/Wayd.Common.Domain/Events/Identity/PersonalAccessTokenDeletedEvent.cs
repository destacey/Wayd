using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A personal access token was deleted. Carries its name because the token is gone by the time anyone reads
/// the entry. Also listed on the owning user's Activity.
/// </summary>
public sealed record PersonalAccessTokenDeletedEvent : DomainEvent<PersonalAccessTokenDeletedEvent>, IDomainEventDescriptor, IRelatedAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    [JsonConstructor]
    public PersonalAccessTokenDeletedEvent(Guid id, string userId, string name, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        UserId = userId;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>The account that owned the token.</summary>
    public string UserId { get; }

    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "PersonalAccessToken";
    [JsonIgnore]
    public Guid AggregateId => Id;
    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new("ApplicationUser", Guid.Parse(UserId))];
}
