using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A personal access token was renamed. Also listed on the owning user's Activity.
/// </summary>
public sealed record PersonalAccessTokenRenamedEvent : DomainEvent<PersonalAccessTokenRenamedEvent>, IDomainEventDescriptor, IRelatedAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public PersonalAccessTokenRenamedEvent(Guid id, string userId, string previousName, string name, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        UserId = userId;
        PreviousName = previousName;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>The account that owns the token.</summary>
    public string UserId { get; }

    public string PreviousName { get; }
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "PersonalAccessToken";
    [JsonIgnore]
    public Guid AggregateId => Id;
    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new("ApplicationUser", Guid.Parse(UserId))];
}
