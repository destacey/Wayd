using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A stretch of days an employee was recorded as away was removed, so they count as available on them again.
/// </summary>
public sealed record EmployeeTimeAwayRemovedEvent : DomainEvent<EmployeeTimeAwayRemovedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public EmployeeTimeAwayRemovedEvent(Guid id, Guid timeAwayId, LocalDate start, LocalDate end, int percentAway, EventActor actor, Instant timestamp)
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

    /// <summary>The time away that was removed.</summary>
    public Guid TimeAwayId { get; }

    /// <summary>The first day the employee had been recorded as away.</summary>
    public LocalDate Start { get; }

    /// <summary>The last day the employee had been recorded as away.</summary>
    public LocalDate End { get; }

    /// <summary>The share of each of those days the employee had been recorded as away, from 1 to 100.</summary>
    public int PercentAway { get; }

    [JsonIgnore]
    public string AggregateType => "Employee";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
