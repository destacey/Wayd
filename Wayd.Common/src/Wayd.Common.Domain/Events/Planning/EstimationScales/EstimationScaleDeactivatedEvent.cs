using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.EstimationScales;

/// <summary>
/// An estimation scale was deactivated. Nothing new can choose it; what already uses it keeps its version.
/// </summary>
public sealed record EstimationScaleDeactivatedEvent : DomainEvent<EstimationScaleDeactivatedEvent>, IDomainEventDescriptor, IEstimationScaleEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    [JsonConstructor]
    public EstimationScaleDeactivatedEvent(
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
