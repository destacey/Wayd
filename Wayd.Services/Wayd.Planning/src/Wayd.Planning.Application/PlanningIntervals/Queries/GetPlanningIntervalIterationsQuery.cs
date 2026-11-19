using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

public sealed record GetPlanningIntervalIterationsQuery : IQuery<IReadOnlyList<PlanningIntervalIterationListDto>>
{
    public GetPlanningIntervalIterationsQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<PlanningInterval>();
    }

    public Expression<Func<PlanningInterval, bool>> IdOrKeyFilter { get; }
}

public sealed class GetPlanningIntervalIterationsQueryHandler(IPlanningDbContext planningDbContext, ISystemCalendar systemCalendar) : IQueryHandler<GetPlanningIntervalIterationsQuery, IReadOnlyList<PlanningIntervalIterationListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ISystemCalendar _systemCalendar = systemCalendar;

    public async Task<IReadOnlyList<PlanningIntervalIterationListDto>> Handle(GetPlanningIntervalIterationsQuery request, CancellationToken cancellationToken)
    {
        LocalDate today = await _systemCalendar.Today(cancellationToken);
        var config = PlanningIntervalIterationListDto.CreateTypeAdapterConfig(today);

        var iterations = await _planningDbContext.PlanningIntervals
            .Where(request.IdOrKeyFilter)
            .SelectMany(p => p.Iterations)
            .ProjectToType<PlanningIntervalIterationListDto>(config)
            .ToListAsync(cancellationToken);

        return [.. iterations.OrderBy(i => i.Start).ThenBy(i => i.Key)];
    }
}
