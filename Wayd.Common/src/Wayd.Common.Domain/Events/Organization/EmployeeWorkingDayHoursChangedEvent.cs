using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// An employee was given a working day of their own, a different one, or went back to the system's.
/// </summary>
public sealed record EmployeeWorkingDayHoursChangedEvent : DomainEvent<EmployeeWorkingDayHoursChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public EmployeeWorkingDayHoursChangedEvent(Guid id, decimal? previousWorkingDayHours, decimal? workingDayHours, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        PreviousWorkingDayHours = previousWorkingDayHours;
        WorkingDayHours = workingDayHours;

        Timestamp = timestamp;
    }

    /// <summary>The employee's id.</summary>
    public Guid Id { get; }

    /// <summary>The hours of the employee's working day before the change, or null for the system's.</summary>
    public decimal? PreviousWorkingDayHours { get; }

    /// <summary>The hours of the employee's working day after the change, or null for the system's.</summary>
    public decimal? WorkingDayHours { get; }

    [JsonIgnore]
    public string AggregateType => "Employee";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
