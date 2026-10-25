using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A user created a personal access token.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="PersonalAccessTokenCreatedEventV2"/> replaced it when
/// tokens gained a scope, which <see cref="Scopes"/> could not carry without changing what it means. Kept so every
/// payload written as this type still deserializes into it — its name and members are the contract those payloads
/// were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by PersonalAccessTokenCreatedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record PersonalAccessTokenCreatedEvent : DomainEvent<PersonalAccessTokenCreatedEvent>, IDomainEventDescriptor, IRelatedAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public PersonalAccessTokenCreatedEvent(Guid id, string userId, string name, Instant expiresAt, string? scopes, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        UserId = userId;
        Name = name;
        ExpiresAt = expiresAt;
        Scopes = scopes;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>The account that owns the token.</summary>
    public string UserId { get; }

    public string Name { get; }
    public Instant ExpiresAt { get; }

    /// <summary>The permission names the token is limited to, as a JSON array; null grants all of the owner's.</summary>
    public string? Scopes { get; }

    [JsonIgnore]
    public string AggregateType => "PersonalAccessToken";
    [JsonIgnore]
    public Guid AggregateId => Id;
    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new("ApplicationUser", Guid.Parse(UserId))];
}
