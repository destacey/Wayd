using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// Tracking began for an intake channel that existed before <see cref="IntakeChannelCreatedEvent"/> was recorded.
/// Carries that event's payload, describing the channel as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record IntakeChannelBaselinedEvent : BaselineEvent<IntakeChannelBaselinedEvent, IntakeChannelCreatedEvent>
{
    public IntakeChannelBaselinedEvent(
        Guid id,
        string name,
        string? description,
        int order,
        Instant? recordCreatedOn,
        Guid? recordCreatedById,
        Instant timestamp)
        : base("IntakeChannel", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
    {
        Id = id;
        Name = name;
        Description = description;
        Order = order;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string? Description { get; }
    public int Order { get; }
}
