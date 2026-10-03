using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// An employee was added, by hand, by an import or by a people sync.
/// </summary>
/// <remarks>
/// An employee is a person, so the payload is ids and states only: the activity log cannot be corrected, and a
/// name or address written into it would outlive the person's right to have it removed.
/// </remarks>
public sealed record EmployeeCreatedEvent : DomainEvent<EmployeeCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public EmployeeCreatedEvent(Guid id, int key, Guid? managerId, bool isActive, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ManagerId = managerId;
        IsActive = isActive;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid? ManagerId { get; }
    public bool IsActive { get; }

    [JsonIgnore]
    public string AggregateType => "Employee";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
