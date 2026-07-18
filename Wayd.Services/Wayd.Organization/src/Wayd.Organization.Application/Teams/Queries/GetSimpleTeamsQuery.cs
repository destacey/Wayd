using Wayd.Common.Domain.Interfaces.Organization;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed record GetSimpleTeamsQuery() : IQuery<List<ISimpleTeam>>;

public sealed class GetSimpleTeamsQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetSimpleTeamsQuery, List<ISimpleTeam>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    public async Task<List<ISimpleTeam>> Handle(GetSimpleTeamsQuery request, CancellationToken cancellationToken)
    {
        var teams = await _organizationDbContext.BaseTeams
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. teams.OfType<ISimpleTeam>()];
    }
}
