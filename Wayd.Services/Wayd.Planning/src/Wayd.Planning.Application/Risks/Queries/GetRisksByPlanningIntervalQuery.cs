using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Extensions;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.Risks.Queries;

public sealed record GetRisksByPlanningIntervalQuery : IQuery<IReadOnlyList<RiskListDto>>
{
    public GetRisksByPlanningIntervalQuery(IdOrKey idOrKey, bool includeClosed, Guid? teamId)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<PlanningInterval>();
        IdOrKey = idOrKey;
        IncludeClosed = includeClosed;
        TeamId = teamId;
    }

    public Expression<Func<PlanningInterval, bool>> IdOrKeyFilter { get; }
    public IdOrKey IdOrKey { get; }
    public bool IncludeClosed { get; }
    public Guid? TeamId { get; }
}

internal sealed class GetRisksByPlanningIntervalQueryHandler(IPlanningDbContext planningDbContext, IDispatcher dispatcher) : IQueryHandler<GetRisksByPlanningIntervalQuery, IReadOnlyList<RiskListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<IReadOnlyList<RiskListDto>> Handle(GetRisksByPlanningIntervalQuery request, CancellationToken cancellationToken)
    {
        var teamIds = await _dispatcher.Send(new GetPlanningIntervalTeamsQuery(request.IdOrKey), cancellationToken);
        if (!teamIds.Any())
            return [];

        if (request.TeamId.HasValue && !teamIds.Contains(request.TeamId.Value))
            return [];

        var piDates = await _planningDbContext.PlanningIntervals
            .Where(request.IdOrKeyFilter)
            .Select(p => new
            {
                start = p.DateRange.Start.ToInstant(),
                end = p.DateRange.End.PlusDays(1).ToInstant()
            })
            .SingleAsync(cancellationToken);

        // there is still a gap in these dates

        var query = _planningDbContext.Risks
            .Include(r => r.Team)
            .Include(r => r.Assignee)
            .Where(r => r.ReportedOn <= piDates.end && (!r.ClosedDate.HasValue || piDates.start <= r.ClosedDate.Value))
            .AsQueryable();

        query = request.TeamId.HasValue
            ? query.Where(r => r.TeamId == request.TeamId.Value)
            : query.Where(r => r.TeamId.HasValue && teamIds.ToList().Contains(r.TeamId.Value));

        if (!request.IncludeClosed)
        {
            query = query.Where(r => r.Status != RiskStatus.Closed);
        }

        var risks = await query
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return risks.Adapt<IReadOnlyList<RiskListDto>>();
    }
}
