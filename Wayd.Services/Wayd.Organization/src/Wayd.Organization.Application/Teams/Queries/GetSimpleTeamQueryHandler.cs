using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Interfaces.Organization;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed class GetSimpleTeamQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetSimpleTeamQuery, ISimpleTeam?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<ISimpleTeam?> Handle(GetSimpleTeamQuery request, CancellationToken cancellationToken)
    {
        return await _organizationDbContext.BaseTeams
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
    }
}
