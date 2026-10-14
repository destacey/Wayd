using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// An employee was set to follow a holiday calendar, a different one, or went back to following the system
/// default.
/// </summary>
public sealed record EmployeeHolidayCalendarChangedEvent : DomainEvent<EmployeeHolidayCalendarChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public EmployeeHolidayCalendarChangedEvent(Guid id, Guid? previousHolidayCalendarId, Guid? holidayCalendarId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        PreviousHolidayCalendarId = previousHolidayCalendarId;
        HolidayCalendarId = holidayCalendarId;

        Timestamp = timestamp;
    }

    /// <summary>The employee's id.</summary>
    public Guid Id { get; }

    /// <summary>The calendar the employee followed before the change, or null for the system default.</summary>
    public Guid? PreviousHolidayCalendarId { get; }

    /// <summary>The calendar the employee follows after the change, or null for the system default.</summary>
    public Guid? HolidayCalendarId { get; }

    [JsonIgnore]
    public string AggregateType => "Employee";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
