using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

public sealed record GetPlanningIntervalsQuery() : IQuery<IReadOnlyList<PlanningIntervalListDto>>;

public sealed class GetPlanningIntervalsQueryHandler : IQueryHandler<GetPlanningIntervalsQuery, IReadOnlyList<PlanningIntervalListDto>>
{
    private readonly IPlanningDbContext _planningDbContext;
    private readonly ISystemCalendar _systemCalendar;

    public GetPlanningIntervalsQueryHandler(IPlanningDbContext planningDbContext, ISystemCalendar systemCalendar)
    {
        _planningDbContext = planningDbContext;
        _systemCalendar = systemCalendar;
    }

    public async Task<IReadOnlyList<PlanningIntervalListDto>> Handle(GetPlanningIntervalsQuery request, CancellationToken cancellationToken)
    {
        var today = await _systemCalendar.Today(cancellationToken);

        // Stable, so an MCP caller's offset slices the same list on every call.
        return await _planningDbContext.PlanningIntervals
            .OrderBy(p => p.Key)
            .Select(p => PlanningIntervalListDto.Create(p, today))
            .ToListAsync(cancellationToken);
    }
}
