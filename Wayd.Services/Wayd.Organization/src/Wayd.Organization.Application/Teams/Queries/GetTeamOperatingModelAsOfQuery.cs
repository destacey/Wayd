using Mapster;
using NodaTime;

namespace Wayd.Organization.Application.Teams.Queries;

/// <summary>
/// Gets the operating model for a team. When AsOfDate is null, returns the current model.
/// When AsOfDate is specified, returns the model that was effective on that date.
/// Returns null if no operating model is defined for the team.
/// </summary>
public sealed record GetTeamOperatingModelAsOfQuery(Guid TeamId, LocalDate? AsOfDate = null)
    : IQuery<TeamOperatingModelDetailsDto?>;

public sealed class GetTeamOperatingModelAsOfQueryHandler(IOrganizationDbContext organizationDbContext, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTeamOperatingModelAsOfQuery, TeamOperatingModelDetailsDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<TeamOperatingModelDetailsDto?> Handle(GetTeamOperatingModelAsOfQuery request, CancellationToken cancellationToken)
    {
        var query = _organizationDbContext.Teams
            .Where(t => t.Id == request.TeamId)
            .SelectMany(t => t.OperatingModels, (team, model) => new { team.Id, Model = model });

        if (request.AsOfDate is null)
        {
            // Current model: has started and hasn't ended
            var today = _dateTimeProvider.Today;
            query = query.Where(x =>
                x.Model.DateRange.Start <= today &&
                (x.Model.DateRange.End == null || x.Model.DateRange.End >= today));
        }
        else
        {
            var asOfDate = request.AsOfDate.Value;
            query = query.Where(x =>
                x.Model.DateRange.Start <= asOfDate &&
                (x.Model.DateRange.End == null || x.Model.DateRange.End >= asOfDate));
        }

        var result = await query
            .Select(x => new { x.Id, x.Model })
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
        {
            return null;
        }

        var dto = result.Model.Adapt<TeamOperatingModelDetailsDto>();
        dto.TeamId = result.Id;
        return dto;
    }
}
