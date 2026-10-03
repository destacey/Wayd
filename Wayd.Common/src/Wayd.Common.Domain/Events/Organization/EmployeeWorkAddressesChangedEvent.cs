using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// The set of work addresses the people source reports for an employee changed: one was added or removed, or
/// another became the primary.
/// </summary>
/// <remarks>
/// Carries no addresses, for the reason <see cref="EmployeeDetailsUpdatedEvent"/> carries no values.
/// </remarks>
public sealed record EmployeeWorkAddressesChangedEvent : DomainEvent<EmployeeWorkAddressesChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public EmployeeWorkAddressesChangedEvent(Guid id, EventActor actor, Instant timestamp)
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
