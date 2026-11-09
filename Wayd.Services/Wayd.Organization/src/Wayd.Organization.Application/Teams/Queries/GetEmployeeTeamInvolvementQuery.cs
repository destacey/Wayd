using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Application.Teams.Dtos;

namespace Wayd.Organization.Application.Teams.Queries;

/// <summary>
/// How many active teams and teams of teams an employee belongs to; empty for an employee on none, or one that
/// does not exist.
/// </summary>
public sealed record GetEmployeeTeamInvolvementQuery(Guid EmployeeId) : IQuery<EmployeeTeamInvolvementDto>;

public sealed class GetEmployeeTeamInvolvementQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetEmployeeTeamInvolvementQuery, EmployeeTeamInvolvementDto>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<EmployeeTeamInvolvementDto> Handle(GetEmployeeTeamInvolvementQuery request, CancellationToken cancellationToken)
    {
        // One row per role held, so a member with two roles on a team appears twice.
        var teams = await _organizationDbContext.TeamMembers
            .Where(m => m.EmployeeId == request.EmployeeId && m.Team.IsActive)
            .Select(m => new { m.TeamId, m.Team.Type })
            .Distinct()
            .ToListAsync(cancellationToken);

        return new EmployeeTeamInvolvementDto
        {
            TeamCount = teams.Count(t => t.Type == TeamType.Team),
            TeamOfTeamsCount = teams.Count(t => t.Type == TeamType.TeamOfTeams),
            TeamIds = [.. teams.Select(t => t.TeamId).Order()],
        };
    }
}
