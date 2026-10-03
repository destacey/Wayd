using Wayd.Common.Domain.Employees;

namespace Wayd.Organization.Domain.Models;

public sealed class TeamMember : BaseSoftDeletableEntity
{
    private TeamMember() { }

    private TeamMember(Guid teamId, Guid employeeId, Guid roleId)
    {
        TeamId = teamId;
        EmployeeId = employeeId;
        RoleId = roleId;
    }

    public Guid TeamId { get; private set; }
    public BaseTeam Team { get; private set; } = default!;
    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = default!;
    public Guid RoleId { get; private set; }
    public TeamMemberRole Role { get; private set; } = default!;

    internal static TeamMember Create(Guid teamId, Guid employeeId, Guid roleId)
    {
        return new TeamMember(teamId, employeeId, roleId);
    }
}
