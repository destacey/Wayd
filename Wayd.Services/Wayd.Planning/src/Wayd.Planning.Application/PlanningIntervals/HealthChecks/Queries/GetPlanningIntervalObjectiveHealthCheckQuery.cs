using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.HealthChecks.Queries;

public sealed record GetPlanningIntervalObjectiveHealthCheckQuery(Guid PlanningIntervalObjectiveId, Guid HealthCheckId)
    : IQuery<PlanningIntervalObjectiveHealthCheckDetailsDto?>;

internal sealed class GetPlanningIntervalObjectiveHealthCheckQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetPlanningIntervalObjectiveHealthCheckQuery, PlanningIntervalObjectiveHealthCheckDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public Task<PlanningIntervalObjectiveHealthCheckDetailsDto?> Handle(GetPlanningIntervalObjectiveHealthCheckQuery request, CancellationToken cancellationToken)
    {
        return _planningDbContext.PlanningIntervalObjectiveHealthChecks
            .AsNoTracking()
            .Where(h => h.Id == request.HealthCheckId
                        && h.PlanningIntervalObjectiveId == request.PlanningIntervalObjectiveId)
            .ProjectToType<PlanningIntervalObjectiveHealthCheckDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
