using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// An employee was recorded as away from all their teams for a stretch of days.
/// </summary>
/// <remarks>
/// Carries no reason. Why someone is away is shown only to them and the people they report up to, and an
/// activity entry is read by everyone who can see the employee.
/// </remarks>
public sealed record EmployeeTimeAwayRecordedEvent : DomainEvent<EmployeeTimeAwayRecordedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public EmployeeTimeAwayRecordedEvent(Guid id, Guid timeAwayId, LocalDate start, LocalDate end, int percentAway, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        TimeAwayId = timeAwayId;
        Start = start;
        End = end;
        PercentAway = percentAway;

        Timestamp = timestamp;
    }

    /// <summary>The employee's id.</summary>
    public Guid Id { get; }

    /// <summary>The time away that was recorded.</summary>
    public Guid TimeAwayId { get; }

    /// <summary>The first day away.</summary>
    public LocalDate Start { get; }

    /// <summary>The last day away.</summary>
    public LocalDate End { get; }

    /// <summary>The share of each of those days the employee is away, from 1 to 100.</summary>
    public int PercentAway { get; }

    [JsonIgnore]
    public string AggregateType => "Employee";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
