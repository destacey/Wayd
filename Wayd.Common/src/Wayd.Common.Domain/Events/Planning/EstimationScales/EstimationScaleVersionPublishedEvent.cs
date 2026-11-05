using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.EstimationScales;

/// <summary>
/// An estimation scale's values were changed, publishing them as the scale's next version. The previous
/// version is unchanged, and whatever is pinned to it stays there.
/// </summary>
public sealed record EstimationScaleVersionPublishedEvent : DomainEvent<EstimationScaleVersionPublishedEvent>, IDomainEventDescriptor, IEstimationScaleEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public EstimationScaleVersionPublishedEvent(
        Guid id,
        int key,
        EstimationScaleVersionValues version,
        Guid previousVersionId,
        int previousVersionNumber,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Version = version;
        PreviousVersionId = previousVersionId;
        PreviousVersionNumber = previousVersionNumber;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The version just published, now the scale's current one.</summary>
    public EstimationScaleVersionValues Version { get; }

    /// <summary>The version that was current before this one.</summary>
    public Guid PreviousVersionId { get; }

    /// <summary>The number of the version that was current before this one.</summary>
    public int PreviousVersionNumber { get; }

    [JsonIgnore]
    public string AggregateType => "EstimationScale";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
