using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.EstimationScales;

/// <summary>
/// An inactive estimation scale was activated, making it available to new poker sessions again.
/// </summary>
public sealed record EstimationScaleActivatedEvent : DomainEvent<EstimationScaleActivatedEvent>, IDomainEventDescriptor, IEstimationScaleEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    [JsonConstructor]
    public EstimationScaleActivatedEvent(
        Guid id,
        int key,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    [JsonIgnore]
    public string AggregateType => "EstimationScale";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
