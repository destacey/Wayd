using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.HealthChecks.Queries;

public sealed record GetPlanningIntervalObjectiveHealthChecksQuery(Guid PlanningIntervalObjectiveId)
    : IQuery<IReadOnlyList<PlanningIntervalObjectiveHealthCheckDetailsDto>>;

public sealed class GetPlanningIntervalObjectiveHealthChecksQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetPlanningIntervalObjectiveHealthChecksQuery, IReadOnlyList<PlanningIntervalObjectiveHealthCheckDetailsDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<IReadOnlyList<PlanningIntervalObjectiveHealthCheckDetailsDto>> Handle(GetPlanningIntervalObjectiveHealthChecksQuery request, CancellationToken cancellationToken)
    {
        var healthChecks = await _planningDbContext.PlanningIntervalObjectiveHealthChecks
            .AsNoTracking()
            .Where(h => h.PlanningIntervalObjectiveId == request.PlanningIntervalObjectiveId)
            .OrderByDescending(h => h.ReportedOn)
            .ProjectToType<PlanningIntervalObjectiveHealthCheckDetailsDto>()
            .ToListAsync(cancellationToken);

        return healthChecks;
    }
}
