using NodaTime;

namespace Wayd.Common.Domain.Events.StrategicManagement;

public sealed record StrategicThemeDeletedEvent : DomainEvent
{
    public StrategicThemeDeletedEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
}
