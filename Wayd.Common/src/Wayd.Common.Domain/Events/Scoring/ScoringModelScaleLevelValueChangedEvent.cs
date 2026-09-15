using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A rating level's value, what choosing it contributes to a criterion, was changed.
/// </summary>
public sealed record ScoringModelScaleLevelValueChangedEvent : DomainEvent<ScoringModelScaleLevelValueChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelScaleLevelValueChangedEvent(
        Guid id,
        int key,
        Guid scaleId,
        Guid levelId,
        decimal previousValue,
        decimal value,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ScaleId = scaleId;
        LevelId = levelId;
        PreviousValue = previousValue;
        Value = value;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ScaleId { get; }
    public Guid LevelId { get; }
    public decimal PreviousValue { get; }
    public decimal Value { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
