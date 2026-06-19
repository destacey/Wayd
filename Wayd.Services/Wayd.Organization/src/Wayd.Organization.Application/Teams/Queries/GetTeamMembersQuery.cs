using Mapster;
using Wayd.Organization.Application.Teams.Dtos;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed record GetTeamMembersQuery(Guid TeamId) : IQuery<IReadOnlyList<TeamMemberDto>>;

internal sealed class GetTeamMembersQueryHandler(IOrganizationDbContext organizationDbContext) : IQueryHandler<GetTeamMembersQuery, IReadOnlyList<TeamMemberDto>>
{
    public async Task<IReadOnlyList<TeamMemberDto>> Handle(GetTeamMembersQuery request, CancellationToken cancellationToken)
    {
        var rows = await organizationDbContext.BaseTeams
            .Where(t => t.Id == request.TeamId)
            .SelectMany(t => t.Members)
            .ProjectToType<TeamMemberFlatDto>()
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.Employee.Id)
            .Select(g => new TeamMemberDto
            {
                Employee = g.First().Employee,
                Team = g.First().Team,
                Roles = g.OrderBy(r => r.Role.Name).Select(r => r.Role).ToList(),
            })
            .OrderBy(m => m.Employee.Name)
            .ToList();
    }
}
