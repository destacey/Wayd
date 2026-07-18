using Mapster;

namespace Wayd.Organization.Application.Teams.Queries;

/// <summary>
/// Gets all operating models for a team, ordered by start date descending (most recent first).
/// </summary>
public sealed record GetTeamOperatingModelsQuery(Guid TeamId) : IQuery<IReadOnlyList<TeamOperatingModelDetailsDto>>;

public sealed class GetTeamOperatingModelsQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetTeamOperatingModelsQuery, IReadOnlyList<TeamOperatingModelDetailsDto>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<IReadOnlyList<TeamOperatingModelDetailsDto>> Handle(GetTeamOperatingModelsQuery request, CancellationToken cancellationToken)
    {
        var models = await _organizationDbContext.Teams
            .AsNoTracking()
            .Where(t => t.Id == request.TeamId)
            .SelectMany(t => t.OperatingModels)
            .OrderByDescending(m => m.DateRange.Start)
            .ToListAsync(cancellationToken);

        return [.. models.Select(model =>
        {
            var dto = model.Adapt<TeamOperatingModelDetailsDto>();
            dto.TeamId = request.TeamId;
            return dto;
        })];
    }
}
