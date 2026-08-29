using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

public sealed record ProjectDeletedEvent : DomainEvent
{
    public ProjectDeletedEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
}
