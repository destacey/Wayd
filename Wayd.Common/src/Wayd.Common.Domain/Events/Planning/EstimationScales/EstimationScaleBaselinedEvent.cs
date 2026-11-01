using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.EstimationScales;

/// <summary>
/// Tracking began for an estimation scale that existed before <see cref="EstimationScaleCreatedEvent"/> was
/// recorded. Carries that event's payload, describing the scale as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record EstimationScaleBaselinedEvent : BaselineEvent<EstimationScaleBaselinedEvent, EstimationScaleCreatedEvent>
{
    public EstimationScaleBaselinedEvent(
        Guid id,
        int key,
        string name,
        string? description,
        bool isActive,
        EstimationScaleVersionValues version,
        Instant? recordCreatedOn,
        Guid? recordCreatedById,
        Instant timestamp)
        : base("EstimationScale", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        IsActive = isActive;
        Version = version;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }
    public bool IsActive { get; }
    public EstimationScaleVersionValues Version { get; }
}
