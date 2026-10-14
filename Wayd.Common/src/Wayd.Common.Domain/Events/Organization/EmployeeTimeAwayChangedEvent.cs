using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// The days an employee is away, how much of each day, or the reason was changed.
/// </summary>
/// <remarks>
/// Says whether the reason changed without carrying it, before or after. Why someone is away is shown only
/// to them and the people they report up to, and an activity entry is read by everyone who can see the employee.
/// </remarks>
public sealed record EmployeeTimeAwayChangedEvent : DomainEvent<EmployeeTimeAwayChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public EmployeeTimeAwayChangedEvent(
        Guid id,
        Guid timeAwayId,
        LocalDate start,
        LocalDate end,
        int percentAway,
        LocalDate previousStart,
        LocalDate previousEnd,
        int previousPercentAway,
        bool reasonChanged,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        TimeAwayId = timeAwayId;
        Start = start;
        End = end;
        PercentAway = percentAway;
        PreviousStart = previousStart;
        PreviousEnd = previousEnd;
        PreviousPercentAway = previousPercentAway;
        ReasonChanged = reasonChanged;

        Timestamp = timestamp;
    }

    /// <summary>The employee's id.</summary>
    public Guid Id { get; }

    /// <summary>The time away that was changed.</summary>
    public Guid TimeAwayId { get; }

    /// <summary>The first day away after the change.</summary>
    public LocalDate Start { get; }

    /// <summary>The last day away after the change.</summary>
    public LocalDate End { get; }

    /// <summary>The share of each day the employee is away after the change, from 1 to 100.</summary>
    public int PercentAway { get; }

    /// <summary>The first day away before the change.</summary>
    public LocalDate PreviousStart { get; }

    /// <summary>The last day away before the change.</summary>
    public LocalDate PreviousEnd { get; }

    /// <summary>The share of each day the employee was away before the change.</summary>
    public int PreviousPercentAway { get; }

    /// <summary>Whether the reason was changed.</summary>
    public bool ReasonChanged { get; }

    [JsonIgnore]
    public string AggregateType => "Employee";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
