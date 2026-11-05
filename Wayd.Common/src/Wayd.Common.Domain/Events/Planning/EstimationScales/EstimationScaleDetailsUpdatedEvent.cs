using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.EstimationScales;

/// <summary>
/// An estimation scale was renamed or its description edited. Neither changes what a value means, so no
/// version is published.
/// </summary>
public sealed record EstimationScaleDetailsUpdatedEvent : DomainEvent<EstimationScaleDetailsUpdatedEvent>, IDomainEventDescriptor, IEstimationScaleEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public EstimationScaleDetailsUpdatedEvent(
        Guid id,
        int key,
        string name,
        string? description,
        EstimationScaleDetails previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }

    /// <summary>The details this edit replaced.</summary>
    public EstimationScaleDetails Previous { get; }

    [JsonIgnore]
    public string AggregateType => "EstimationScale";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
