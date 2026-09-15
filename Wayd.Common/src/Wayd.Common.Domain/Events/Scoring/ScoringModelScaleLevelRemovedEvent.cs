using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A rating level was removed from one of a scoring model's scales.
/// </summary>
/// <remarks>
/// The label is carried because the level is gone by the time anyone reads the entry.
/// </remarks>
public sealed record ScoringModelScaleLevelRemovedEvent : DomainEvent<ScoringModelScaleLevelRemovedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelScaleLevelRemovedEvent(
        Guid id,
        int key,
        Guid scaleId,
        Guid levelId,
        string label,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ScaleId = scaleId;
        LevelId = levelId;
        Label = label;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ScaleId { get; }
    public Guid LevelId { get; }
    public string Label { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
