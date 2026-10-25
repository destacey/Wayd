using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Organization.Application.Teams.Queries;

/// <summary>Reads the employee ids of a team's direct members.</summary>
public sealed class GetTeamMemberIdsQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetTeamMemberIdsQuery, IReadOnlyList<Guid>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<IReadOnlyList<Guid>> Handle(GetTeamMemberIdsQuery request, CancellationToken cancellationToken)
    {
        // Distinct: a member holding several roles has a row per role.
        return await _organizationDbContext.BaseTeams
            .Where(t => t.Id == request.TeamId)
            .SelectMany(t => t.Members.Select(m => m.EmployeeId))
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
