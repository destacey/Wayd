using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// An employee started reporting to a different manager, or stopped reporting to one.
/// </summary>
public sealed record EmployeeManagerChangedEvent : DomainEvent<EmployeeManagerChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public EmployeeManagerChangedEvent(Guid id, Guid? previousManagerId, Guid? managerId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        PreviousManagerId = previousManagerId;
        ManagerId = managerId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public Guid? PreviousManagerId { get; }
    public Guid? ManagerId { get; }

    [JsonIgnore]
    public string AggregateType => "Employee";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
