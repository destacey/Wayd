using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

public sealed record TeamActivatedEvent : DomainEvent
{
    public TeamActivatedEvent(Guid id, Instant timestamp)
    {
        Id = id;
        Timestamp = timestamp;
    }

    public Guid Id { get; }
}
