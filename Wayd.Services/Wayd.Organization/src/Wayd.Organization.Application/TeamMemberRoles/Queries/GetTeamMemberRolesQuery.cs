using Mapster;

namespace Wayd.Organization.Application.TeamMemberRoles.Queries;

public sealed record GetTeamMemberRolesQuery(bool IncludeInactive = false) : IQuery<IReadOnlyList<TeamMemberRoleDto>>;

public sealed class GetTeamMemberRolesQueryHandler(IOrganizationDbContext organizationDbContext) : IQueryHandler<GetTeamMemberRolesQuery, IReadOnlyList<TeamMemberRoleDto>>
{
    public async Task<IReadOnlyList<TeamMemberRoleDto>> Handle(GetTeamMemberRolesQuery request, CancellationToken cancellationToken)
    {
        var query = organizationDbContext.TeamMemberRoles.AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(r => r.IsActive);

        return await query
            .OrderBy(r => r.Name)
            .ProjectToType<TeamMemberRoleDto>()
            .ToListAsync(cancellationToken);
    }
}
