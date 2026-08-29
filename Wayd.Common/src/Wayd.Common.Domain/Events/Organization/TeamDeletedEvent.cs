using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

public sealed record TeamDeletedEvent : DomainEvent
{
    public TeamDeletedEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Timestamp = timestamp;
    }

    public Guid Id { get; }
}
