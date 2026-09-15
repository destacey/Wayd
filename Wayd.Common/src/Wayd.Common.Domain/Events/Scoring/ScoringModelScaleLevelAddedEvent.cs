using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A rating level was added to one of a scoring model's scales.
/// </summary>
public sealed record ScoringModelScaleLevelAddedEvent : DomainEvent<ScoringModelScaleLevelAddedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelScaleLevelAddedEvent(
        Guid id,
        int key,
        Guid scaleId,
        Guid levelId,
        string label,
        decimal value,
        int order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ScaleId = scaleId;
        LevelId = levelId;
        Label = label;
        Value = value;
        Order = order;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ScaleId { get; }
    public Guid LevelId { get; }
    public string Label { get; }
    public decimal Value { get; }
    public int Order { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
