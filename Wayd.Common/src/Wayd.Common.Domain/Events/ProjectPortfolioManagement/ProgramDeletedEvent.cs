using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

public sealed record ProgramDeletedEvent : DomainEvent
{
    public ProgramDeletedEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
}
