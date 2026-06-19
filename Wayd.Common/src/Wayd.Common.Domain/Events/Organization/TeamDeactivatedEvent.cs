using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

public sealed record TeamDeactivatedEvent : DomainEvent
{
    public TeamDeactivatedEvent(Guid id, LocalDate inactiveDate, Instant timestamp)
    {
        Id = id;
        InactiveDate = inactiveDate;
        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public LocalDate InactiveDate { get; }
}
