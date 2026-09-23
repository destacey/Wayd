using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed class GetTeamMemberCountQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetTeamMemberCountQuery, int?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<int?> Handle(GetTeamMemberCountQuery request, CancellationToken cancellationToken)
    {
        // A member holds one row per role, so employees are counted, not rows.
        return await _organizationDbContext.BaseTeams
            .Where(t => t.Id == request.TeamId)
            .Select(t => (int?)t.Members
                .Select(m => m.EmployeeId)
                .Distinct()
                .Count())
            .FirstOrDefaultAsync(cancellationToken);
    }
}
