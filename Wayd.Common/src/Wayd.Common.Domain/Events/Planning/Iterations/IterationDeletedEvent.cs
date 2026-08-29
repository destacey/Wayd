using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

public sealed record IterationDeletedEvent : DomainEvent
{
    public IterationDeletedEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
}
