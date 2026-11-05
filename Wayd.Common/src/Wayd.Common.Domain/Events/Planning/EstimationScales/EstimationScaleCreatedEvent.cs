using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.EstimationScales;

/// <summary>
/// An estimation scale was created, active and with its first version published.
/// </summary>
public sealed record EstimationScaleCreatedEvent : DomainEvent<EstimationScaleCreatedEvent>, IDomainEventDescriptor, IEstimationScaleEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public EstimationScaleCreatedEvent(
        Guid id,
        int key,
        string name,
        string? description,
        bool isActive,
        EstimationScaleVersionValues version,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        IsActive = isActive;
        Version = version;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }
    public bool IsActive { get; }

    /// <summary>The scale's current version.</summary>
    public EstimationScaleVersionValues Version { get; }

    [JsonIgnore]
    public string AggregateType => "EstimationScale";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
