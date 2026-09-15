using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A scoring model's outputs were put in a different evaluation order.
/// </summary>
public sealed record ScoringModelOutputsReorderedEvent : DomainEvent<ScoringModelOutputsReorderedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelOutputsReorderedEvent(
        Guid id,
        int key,
        Guid[] previousOrder,
        Guid[] order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousOrder = [.. previousOrder];
        Order = [.. order];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The output ids in their order before the change.</summary>
    public Guid[] PreviousOrder { get; }

    /// <summary>The output ids in their order after the change.</summary>
    public Guid[] Order { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
