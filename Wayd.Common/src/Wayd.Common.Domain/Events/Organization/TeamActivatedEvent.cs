using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

public sealed record TeamActivatedEvent : DomainEvent
{
    public TeamActivatedEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Timestamp = timestamp;
    }

    public Guid Id { get; }
}
