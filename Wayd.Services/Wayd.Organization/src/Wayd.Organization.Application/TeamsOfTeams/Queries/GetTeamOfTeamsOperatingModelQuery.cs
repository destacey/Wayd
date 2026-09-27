using Mapster;

namespace Wayd.Organization.Application.TeamsOfTeams.Queries;

/// <summary>
/// Gets a specific operating model for a team of teams, or null when it does not exist.
/// </summary>
public sealed record GetTeamOfTeamsOperatingModelQuery(Guid TeamId, Guid OperatingModelId)
    : IQuery<TeamOfTeamsOperatingModelDetailsDto?>;

public sealed class GetTeamOfTeamsOperatingModelQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetTeamOfTeamsOperatingModelQuery, TeamOfTeamsOperatingModelDetailsDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<TeamOfTeamsOperatingModelDetailsDto?> Handle(GetTeamOfTeamsOperatingModelQuery request, CancellationToken cancellationToken)
    {
        var model = await _organizationDbContext.TeamOfTeams
            .AsNoTracking()
            .Where(t => t.Id == request.TeamId)
            .SelectMany(t => t.OperatingModels)
            .Where(m => m.Id == request.OperatingModelId)
            .FirstOrDefaultAsync(cancellationToken);

        if (model is null)
            return null;

        var dto = model.Adapt<TeamOfTeamsOperatingModelDetailsDto>();
        dto.TeamId = request.TeamId;
        return dto;
    }
}
