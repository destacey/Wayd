using NodaTime;
using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// A stretch of days an employee is away from every team they are allocated to, for all or part of each day:
/// leave, business travel, training. Changed only through its <see cref="EmployeeCapacity"/>.
/// </summary>
/// <remarks>
/// Not an allocation: it takes nothing from the employee's 100%, and lowers their availability to each team
/// in proportion to what that team has. Being away from one team only is a change to that team's allocation.
/// </remarks>
public sealed class EmployeeTimeAway : BaseAuditableEntity
{
    /// <summary>The whole of a day, as a percent.</summary>
    public const int FullDay = 100;

    private EmployeeTimeAway() { }

    internal EmployeeTimeAway(Guid employeeId, LocalDate start, LocalDate end, int percentAway, TimeAwayReason reason)
    {
        EmployeeId = employeeId;
        Start = start;
        End = end;
        PercentAway = percentAway;
        Reason = reason;
    }

    /// <summary>The employee who is away.</summary>
    public Guid EmployeeId { get; private set; }

    /// <summary>The first day away.</summary>
    public LocalDate Start { get; private set; }

    /// <summary>The last day away.</summary>
    public LocalDate End { get; private set; }

    /// <summary>The share of each day the employee is away, from 1 to 100.</summary>
    public int PercentAway { get; private set; }

    /// <summary>
    /// Why the employee is away. Read only by the employee, the people they report up to and whoever
    /// administers time away.
    /// </summary>
    public TimeAwayReason Reason { get; private set; }

    /// <summary>Whether the employee is away on <paramref name="day"/>.</summary>
    public bool Covers(LocalDate day) => Start <= day && day <= End;

    internal void Change(LocalDate start, LocalDate end, int percentAway, TimeAwayReason reason)
    {
        Start = start;
        End = end;
        PercentAway = percentAway;
        Reason = reason;
    }
}
