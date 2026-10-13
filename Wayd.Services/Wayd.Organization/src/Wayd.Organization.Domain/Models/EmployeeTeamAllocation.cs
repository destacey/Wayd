using NodaTime;
using Wayd.Common.Domain.Events.Organization;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// A share of an employee's time given to a team, in one role, for a stretch of days. Changed only through
/// its <see cref="EmployeeCapacity"/>, which keeps the employee's allocations within 100% on every day.
/// </summary>
public sealed class EmployeeTeamAllocation : BaseAuditableEntity
{
    private EmployeeTeamAllocation() { }

    internal EmployeeTeamAllocation(Guid employeeId, EmployeeAllocationDetails details)
    {
        EmployeeId = employeeId;
        TeamId = details.TeamId;
        RoleId = details.RoleId;
        Start = details.Start;
        End = details.End;
        Percent = details.Percent;
    }

    /// <summary>The employee whose time this is.</summary>
    public Guid EmployeeId { get; private set; }

    /// <summary>The team or team of teams the time is given to. The employee need not be a member of it.</summary>
    public Guid TeamId { get; private set; }

    /// <summary>
    /// The team member role the time is spent in, or null when none was chosen. Independent of the roles the
    /// employee holds as a member of the team.
    /// </summary>
    public Guid? RoleId { get; private set; }

    /// <summary>The first day of the allocation.</summary>
    public LocalDate Start { get; private set; }

    /// <summary>The last day of the allocation, or null when it has no end.</summary>
    public LocalDate? End { get; private set; }

    /// <summary>The share of the employee's time, from 1 to 100.</summary>
    public int Percent { get; private set; }

    internal EmployeeAllocationDetails ToDetails() => new(TeamId, RoleId, Start, End, Percent);

    internal void Resize(LocalDate? end, int percent)
    {
        End = end;
        Percent = percent;
    }
}
