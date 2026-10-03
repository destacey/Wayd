using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed class IsTeamMemberQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<IsTeamMemberQuery, bool>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<bool> Handle(IsTeamMemberQuery request, CancellationToken cancellationToken)
    {
        var asOf = request.AsOf;
        var employeeId = request.EmployeeId;

        return await _organizationDbContext.BaseTeams
            .Where(t => t.Id == request.TeamId)
            .AnyAsync(t => t.Members.Any(m => m.EmployeeId == employeeId)
                || t.ParentMemberships.Any(pm => pm.DateRange.Start <= asOf
                    && (pm.DateRange.End == null || pm.DateRange.End >= asOf)
                    && pm.Target.Members.Any(m => m.EmployeeId == employeeId)),
                cancellationToken);
    }
}
