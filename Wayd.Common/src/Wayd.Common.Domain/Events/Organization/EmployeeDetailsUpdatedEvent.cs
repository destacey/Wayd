using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// An employee's descriptive details changed: their name, employee number, hire date, primary email, job title,
/// department, office location or employee type.
/// </summary>
/// <remarks>
/// Carries no values, before or after. Every one of those fields describes a person, and the activity log
/// cannot be corrected; a consumer that needs the current values reads the employee.
/// </remarks>
public sealed record EmployeeDetailsUpdatedEvent : DomainEvent<EmployeeDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public EmployeeDetailsUpdatedEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    [JsonIgnore]
    public string AggregateType => "Employee";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
