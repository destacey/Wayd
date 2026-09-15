using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A rating level's label was changed.
/// </summary>
/// <remarks>
/// Separate from <see cref="ScoringModelScaleLevelValueChangedEvent"/>: a label is what a scorer reads, and a value is what a rating contributes to the score.
/// </remarks>
public sealed record ScoringModelScaleLevelRelabeledEvent : DomainEvent<ScoringModelScaleLevelRelabeledEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelScaleLevelRelabeledEvent(
        Guid id,
        int key,
        Guid scaleId,
        Guid levelId,
        string previousLabel,
        string label,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ScaleId = scaleId;
        LevelId = levelId;
        PreviousLabel = previousLabel;
        Label = label;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ScaleId { get; }
    public Guid LevelId { get; }
    public string PreviousLabel { get; }
    public string Label { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
