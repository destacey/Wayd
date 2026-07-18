using Wayd.Common.Application.Requests.Goals.Dtos;
using Wayd.Common.Application.Requests.Goals.Queries;
using Wayd.Goals.Application.Persistence;

namespace Wayd.Goals.Application.Objectives.Queries;

public sealed class GetObjectivesForPlanningIntervalsQueryHandler(IGoalsDbContext goalsDbContext) : IQueryHandler<GetObjectivesForPlanningIntervalsQuery, IReadOnlyList<ObjectiveListDto>>
{
    private readonly IGoalsDbContext _goalsDbContext = goalsDbContext;

    public async Task<IReadOnlyList<ObjectiveListDto>> Handle(GetObjectivesForPlanningIntervalsQuery request, CancellationToken cancellationToken)
    {
        var query = _goalsDbContext.Objectives
            .Where(o => o.PlanId.HasValue && request.PlanningIntervalIds.Contains(o.PlanId.Value))
            .AsQueryable();

        if (request.TeamIds?.Any() ?? false)
        {
            query = query.Where(o => o.OwnerId.HasValue && request.TeamIds.Contains(o.OwnerId.Value));
        }

        return await query
            .ProjectToType<ObjectiveListDto>()
            .ToListAsync(cancellationToken);
    }
}
