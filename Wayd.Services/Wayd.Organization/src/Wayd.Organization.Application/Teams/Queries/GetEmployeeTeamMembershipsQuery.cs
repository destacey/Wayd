using Mapster;
using Wayd.Organization.Application.Teams.Dtos;

namespace Wayd.Organization.Application.Teams.Queries;

/// <summary>
/// The teams and teams of teams an employee belongs to, with the roles they hold on each; null when no employee
/// has <paramref name="EmployeeId"/>.
/// </summary>
public sealed record GetEmployeeTeamMembershipsQuery(Guid EmployeeId) : IQuery<IReadOnlyList<TeamMemberDto>?>;

public sealed class GetEmployeeTeamMembershipsQueryHandler(IOrganizationDbContext organizationDbContext) : IQueryHandler<GetEmployeeTeamMembershipsQuery, IReadOnlyList<TeamMemberDto>?>
{
    public async Task<IReadOnlyList<TeamMemberDto>?> Handle(GetEmployeeTeamMembershipsQuery request, CancellationToken cancellationToken)
    {
        if (!await organizationDbContext.Employees.AnyAsync(e => e.Id == request.EmployeeId, cancellationToken))
            return null;

        var rows = await organizationDbContext.TeamMembers
            .Where(m => m.EmployeeId == request.EmployeeId)
            .ProjectToType<TeamMemberFlatDto>()
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.Team.Id)
            .Select(g => new TeamMemberDto
            {
                Employee = g.First().Employee,
                Team = g.First().Team,
                Roles = g.OrderBy(r => r.Role.Name).Select(r => r.Role).ToList(),
            })
            .OrderBy(m => m.Team.Name)
            .ThenBy(m => m.Team.Id)
            .ToList();
    }
}
