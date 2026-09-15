using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// The rating levels of one of a scoring model's scales were put in a different order.
/// </summary>
public sealed record ScoringModelScaleLevelsReorderedEvent : DomainEvent<ScoringModelScaleLevelsReorderedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelScaleLevelsReorderedEvent(
        Guid id,
        int key,
        Guid scaleId,
        Guid[] previousOrder,
        Guid[] order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ScaleId = scaleId;
        PreviousOrder = [.. previousOrder];
        Order = [.. order];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ScaleId { get; }

    /// <summary>The level ids in their order before the change.</summary>
    public Guid[] PreviousOrder { get; }

    /// <summary>The level ids in their order after the change.</summary>
    public Guid[] Order { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
