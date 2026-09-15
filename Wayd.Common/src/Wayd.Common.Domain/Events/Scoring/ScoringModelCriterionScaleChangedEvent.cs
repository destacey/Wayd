using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A criterion was moved to a different rating scale, or to or from free numeric entry.
/// </summary>
public sealed record ScoringModelCriterionScaleChangedEvent : DomainEvent<ScoringModelCriterionScaleChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelCriterionScaleChangedEvent(
        Guid id,
        int key,
        Guid criterionId,
        Guid? previousScaleId,
        Guid? scaleId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        CriterionId = criterionId;
        PreviousScaleId = previousScaleId;
        ScaleId = scaleId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid CriterionId { get; }

    /// <summary>The scale it was rated against, or null for free numeric entry.</summary>
    public Guid? PreviousScaleId { get; }

    /// <summary>The scale it is now rated against, or null for free numeric entry.</summary>
    public Guid? ScaleId { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
