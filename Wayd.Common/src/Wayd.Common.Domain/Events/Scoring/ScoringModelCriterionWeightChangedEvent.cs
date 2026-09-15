using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A criterion's weight was changed.
/// </summary>
public sealed record ScoringModelCriterionWeightChangedEvent : DomainEvent<ScoringModelCriterionWeightChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelCriterionWeightChangedEvent(
        Guid id,
        int key,
        Guid criterionId,
        decimal? previousWeight,
        decimal? weight,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        CriterionId = criterionId;
        PreviousWeight = previousWeight;
        Weight = weight;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid CriterionId { get; }
    public decimal? PreviousWeight { get; }
    public decimal? Weight { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
