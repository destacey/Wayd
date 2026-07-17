using Mapster;

namespace Wayd.Organization.Application.TeamMemberRoles.Queries;

public sealed record GetTeamMemberRoleQuery(Guid Id) : IQuery<TeamMemberRoleDto?>;

public sealed class GetTeamMemberRoleQueryHandler(IOrganizationDbContext organizationDbContext) : IQueryHandler<GetTeamMemberRoleQuery, TeamMemberRoleDto?>
{
    public async Task<TeamMemberRoleDto?> Handle(GetTeamMemberRoleQuery request, CancellationToken cancellationToken)
    {
        return await organizationDbContext.TeamMemberRoles
            .Where(r => r.Id == request.Id)
            .ProjectToType<TeamMemberRoleDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
