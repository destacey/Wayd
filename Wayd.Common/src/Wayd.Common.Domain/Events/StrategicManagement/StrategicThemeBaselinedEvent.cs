using Wayd.Common.Domain.Enums.StrategicManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.StrategicManagement;

/// <summary>
/// Tracking began for a strategic theme that existed before <see cref="StrategicThemeCreatedEvent"/> was
/// recorded. Carries that event's payload, describing the theme as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record StrategicThemeBaselinedEvent : BaselineEvent<StrategicThemeBaselinedEvent, StrategicThemeCreatedEvent>
{
    public StrategicThemeBaselinedEvent(Guid id, int key, string name, string description, StrategicThemeState state, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp)
        : base("StrategicTheme", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        State = state;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string Description { get; }
    public StrategicThemeState State { get; }
}
