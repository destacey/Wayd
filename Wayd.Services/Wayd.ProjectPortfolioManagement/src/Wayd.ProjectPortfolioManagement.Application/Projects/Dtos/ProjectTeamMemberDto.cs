using Wayd.Common.Application.Employees.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

/// <summary>
/// Represents a team member on a project with their roles, assigned phases, and active workload.
/// </summary>
public sealed record ProjectTeamMemberDto
{
    /// <summary>
    /// The employee.
    /// </summary>
    public required EmployeeNavigationDto Employee { get; init; }

    /// <summary>
    /// The project roles assigned to this team member (e.g., Sponsor, Owner, Manager, Member).
    /// </summary>
    public required List<string> Roles { get; init; } = [];

    /// <summary>
    /// The names of the phases this team member is assigned to, ordered by phase order.
    /// </summary>
    public required List<string> AssignedPhases { get; init; } = [];

    /// <summary>
    /// The number of active leaf tasks (Not Started or In Progress) assigned to this team member.
    /// </summary>
    public int ActiveWorkItemCount { get; init; }
}
