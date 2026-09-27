using Mapster;

namespace Wayd.Organization.Application.TeamsOfTeams.Queries;

/// <summary>
/// Gets all operating models for a team of teams, ordered by start date descending (most recent first).
/// </summary>
public sealed record GetTeamOfTeamsOperatingModelsQuery(Guid TeamId) : IQuery<IReadOnlyList<TeamOfTeamsOperatingModelDetailsDto>>;

public sealed class GetTeamOfTeamsOperatingModelsQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetTeamOfTeamsOperatingModelsQuery, IReadOnlyList<TeamOfTeamsOperatingModelDetailsDto>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<IReadOnlyList<TeamOfTeamsOperatingModelDetailsDto>> Handle(GetTeamOfTeamsOperatingModelsQuery request, CancellationToken cancellationToken)
    {
        var models = await _organizationDbContext.TeamOfTeams
            .AsNoTracking()
            .Where(t => t.Id == request.TeamId)
            .SelectMany(t => t.OperatingModels)
            .OrderByDescending(m => m.DateRange.Start)
            .ToListAsync(cancellationToken);

        return [.. models.Select(model =>
        {
            var dto = model.Adapt<TeamOfTeamsOperatingModelDetailsDto>();
            dto.TeamId = request.TeamId;
            return dto;
        })];
    }
}
