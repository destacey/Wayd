using Mapster;

namespace Wayd.Organization.Application.Teams.Queries;

/// <summary>
/// Gets a specific operating model for a team. 
/// Returns null if no operating model is defined for the team.
/// </summary>
public sealed record GetTeamOperatingModelQuery(Guid TeamId, Guid TeamOperatingModelId)
    : IQuery<TeamOperatingModelDetailsDto?>;

public sealed class GetTeamOperatingModelQueryHandler(IOrganizationDbContext organizationDbContext, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTeamOperatingModelQuery, TeamOperatingModelDetailsDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<TeamOperatingModelDetailsDto?> Handle(GetTeamOperatingModelQuery request, CancellationToken cancellationToken)
    {
        var model = await _organizationDbContext.Teams
            .AsNoTracking()
            .Where(t => t.Id == request.TeamId)
            .SelectMany(t => t.OperatingModels)
            .Where(m => m.Id == request.TeamOperatingModelId)
            .FirstOrDefaultAsync(cancellationToken);

        if (model is null)
        {
            return null;
        }

        var dto = model.Adapt<TeamOperatingModelDetailsDto>();
        dto.TeamId = request.TeamId;
        return dto;
    }
}
