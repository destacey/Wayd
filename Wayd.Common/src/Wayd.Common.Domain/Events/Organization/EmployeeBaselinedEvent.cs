using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// Tracking began for an employee that existed before <see cref="EmployeeCreatedEvent"/> was recorded. Carries
/// that event's payload, describing the employee as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record EmployeeBaselinedEvent : BaselineEvent<EmployeeBaselinedEvent, EmployeeCreatedEvent>
{
    public EmployeeBaselinedEvent(Guid id, int key, Guid? managerId, bool isActive, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp)
        : base("Employee", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
    {
        Id = id;
        Key = key;
        ManagerId = managerId;
        IsActive = isActive;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid? ManagerId { get; }
    public bool IsActive { get; }
}
