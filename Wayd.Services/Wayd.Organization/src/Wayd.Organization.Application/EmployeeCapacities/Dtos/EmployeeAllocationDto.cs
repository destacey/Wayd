using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Organization.Application.Models;

namespace Wayd.Organization.Application.EmployeeCapacities.Dtos;

/// <summary>A share of an employee's time given to a team, in one role, for a stretch of days.</summary>
public sealed record EmployeeAllocationDto
{
    public required Guid Id { get; set; }

    /// <summary>The team or team of teams the time is given to.</summary>
    public required TeamNavigationDto Team { get; set; }

    /// <summary>The team member role the time is spent in, or null when none was chosen.</summary>
    public NavigationDto? Role { get; set; }

    /// <summary>The first day of the allocation.</summary>
    public required LocalDate Start { get; set; }

    /// <summary>The last day of the allocation, or null when it has no end.</summary>
    public LocalDate? End { get; set; }

    /// <summary>The share of the employee's time, from 1 to 100.</summary>
    public required int Percent { get; set; }

    /// <summary>Whether the caller may change this team's allocations.</summary>
    public bool CanManage { get; set; }
}
