using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// An active scoring model was archived and can no longer be assigned.
/// </summary>
/// <remarks>
/// Only an active model can be archived, so the type records both ends of the transition. Scores already recorded against it are unaffected.
/// </remarks>
public sealed record ScoringModelArchivedEvent : DomainEvent<ScoringModelArchivedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    [JsonConstructor]
    public ScoringModelArchivedEvent(Guid id, int key, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
