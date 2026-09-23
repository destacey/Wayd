using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed class GetTeamMemberCountQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetTeamMemberCountQuery, int?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<int?> Handle(GetTeamMemberCountQuery request, CancellationToken cancellationToken)
    {
        var memberIds = await _organizationDbContext.BaseTeams
            .Where(t => t.Id == request.TeamId)
            .Select(t => t.Members.Select(m => m.EmployeeId).ToList())
            .FirstOrDefaultAsync(cancellationToken);

        if (memberIds is null)
            return null;

        // Counting employees rather than member rows counts a member with several roles once.
        return await _organizationDbContext.Employees
            .CountAsync(e => e.IsActive && memberIds.Contains(e.Id), cancellationToken);
    }
}
