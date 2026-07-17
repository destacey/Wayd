using Mapster;
using NodaTime;

namespace Wayd.Organization.Application.Teams.Queries;

/// <summary>
/// Gets the operating models for multiple teams. When AsOfDate is null, returns current models.
/// When AsOfDate is specified, returns the models that were effective on that date.
/// Teams without an operating model are not included in the result dictionary.
/// </summary>
public sealed record GetTeamOperatingModelsForTeamsQuery(IEnumerable<Guid> TeamIds, LocalDate? AsOfDate = null)
    : IQuery<List<TeamOperatingModelDetailsDto>>;

public sealed class GetTeamOperatingModelsForTeamsQueryHandler(IOrganizationDbContext organizationDbContext, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTeamOperatingModelsForTeamsQuery, List<TeamOperatingModelDetailsDto>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<List<TeamOperatingModelDetailsDto>> Handle(GetTeamOperatingModelsForTeamsQuery request, CancellationToken cancellationToken)
    {
        var teamIdList = request.TeamIds.ToList();
        if (teamIdList.Count == 0)
        {
            return [];
        }

        var asOfDate = request.AsOfDate ?? _dateTimeProvider.Today;

        var query = _organizationDbContext.Teams
            .Where(t => teamIdList.Contains(t.Id))
            .SelectMany(t => t.OperatingModels, (team, model) => new { team.Id, Model = model });

        query = query.Where(x =>
            x.Model.DateRange.Start <= asOfDate &&
            (x.Model.DateRange.End == null || x.Model.DateRange.End >= asOfDate));

        var results = await query
            .Select(x => new { x.Id, x.Model })
            .ToListAsync(cancellationToken);

        return [.. results.Select(result =>
        {
            var dto = result.Model.Adapt<TeamOperatingModelDetailsDto>();
            dto.TeamId = result.Id;
            return dto;
        })];
    }
}
