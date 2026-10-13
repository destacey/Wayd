using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed class GetTeamsWithMemberQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetTeamsWithMemberQuery, IReadOnlySet<Guid>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<IReadOnlySet<Guid>> Handle(GetTeamsWithMemberQuery request, CancellationToken cancellationToken)
    {
        var asOf = request.AsOf;
        var employeeId = request.EmployeeId;
        var teamIds = request.TeamIds;

        var teams = await _organizationDbContext.BaseTeams
            .Where(t => teamIds.Contains(t.Id))
            .Where(t => t.Members.Any(m => m.EmployeeId == employeeId)
                || t.ParentMemberships.Any(pm => pm.DateRange.Start <= asOf
                    && (pm.DateRange.End == null || pm.DateRange.End >= asOf)
                    && pm.Target.Members.Any(m => m.EmployeeId == employeeId)))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        return teams.ToHashSet();
    }
}
