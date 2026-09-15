using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A different output, or none, became the scoring model's primary score.
/// </summary>
/// <remarks>
/// Raised whenever the primary moves, whichever call moved it: adding the first output or one marked primary, editing an output's primary flag, or removing the primary output.
/// </remarks>
public sealed record ScoringModelPrimaryOutputChangedEvent : DomainEvent<ScoringModelPrimaryOutputChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelPrimaryOutputChangedEvent(
        Guid id,
        int key,
        Guid? previousOutputId,
        Guid? outputId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousOutputId = previousOutputId;
        OutputId = outputId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The output that was primary, or null when none was.</summary>
    public Guid? PreviousOutputId { get; }

    /// <summary>The output that is now primary, or null when none is.</summary>
    public Guid? OutputId { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
