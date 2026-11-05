using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Extensions.Organizations;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.Organization.Application.Teams.Queries;

/// <summary>Reads the teams and teams of teams named by id or by code, in one query.</summary>
public sealed class GetTeamsByIdOrCodeQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetTeamsByIdOrCodeQuery, IReadOnlyList<ISimpleTeam>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<IReadOnlyList<ISimpleTeam>> Handle(GetTeamsByIdOrCodeQuery request, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Where(id => id != Guid.Empty).Distinct().ToList();

        // Compared as TeamCode instances, never t.Code.Value: Code is a value converter, so the property
        // translates but a member of it does not. A malformed value would throw in the constructor, and no team
        // can hold one, so it is dropped here.
        var codes = request.Codes
            .Select(c => c?.Trim().ToUpperInvariant())
            .Where(c => c.IsValidTeamCodeFormat())
            .Distinct()
            .Select(c => new TeamCode(c!))
            .ToList();

        if (ids.Count == 0 && codes.Count == 0)
        {
            return [];
        }

        return await _organizationDbContext.BaseTeams
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id) || codes.Contains(t.Code))
            .OrderBy(t => t.Key)
            .ToListAsync(cancellationToken);
    }
}
