using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.Organizations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A team was deleted.
/// </summary>
/// <remarks>
/// Nothing raises it and nothing handles it: a team or team of teams cannot be deleted, only deactivated. The
/// type is kept because a published event is never removed. A delete added later must raise it from the team
/// and give each module keeping a team copy a handler before it ships.
/// </remarks>
public sealed record TeamDeletedEvent : DomainEvent<TeamDeletedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    public TeamDeletedEvent(Guid id, int key, TeamCode? code, EventActor actor, Instant timestamp)
        : base(actor, "1.1")
    {
        Id = id;
        Key = key;
        Code = code;
        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>Added in 1.0 -> 1.1. Zero on a payload written before 1.1, which did not record it.</summary>
    public int Key { get; }

    /// <summary>Added in 1.0 -> 1.1. Null on a payload written before 1.1, which did not record it.</summary>
    public TeamCode? Code { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
