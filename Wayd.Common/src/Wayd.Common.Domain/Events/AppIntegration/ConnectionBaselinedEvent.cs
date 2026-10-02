using NodaTime;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// Tracking began for a connection that existed before <see cref="ConnectionCreatedEvent"/> was recorded. Carries
/// that event's payload, describing the connection as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record ConnectionBaselinedEvent : BaselineEvent<ConnectionBaselinedEvent, ConnectionCreatedEvent>
{
    public ConnectionBaselinedEvent(
        Guid id,
        string name,
        string? description,
        Connector connector,
        bool isActive,
        ConnectionSetting[] settings,
        Instant? recordCreatedOn,
        Guid? recordCreatedById,
        Instant timestamp)
        : base("Connection", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
    {
        Id = id;
        Name = name;
        Description = description;
        Connector = connector;
        IsActive = isActive;
        Settings = [.. settings];
    }

    public Guid Id { get; }
    public string Name { get; }
    public string? Description { get; }
    public Connector Connector { get; }
    public bool IsActive { get; }
    public ConnectionSetting[] Settings { get; }
}
