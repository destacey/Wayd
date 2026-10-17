using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.HealthChecks.Queries;

/// <summary>
/// One health check, or <c>null</c> when it does not belong to the objective or the objective is not in the
/// given planning interval.
/// </summary>
public sealed record GetPlanningIntervalObjectiveHealthCheckQuery(Guid PlanningIntervalId, Guid PlanningIntervalObjectiveId, Guid HealthCheckId)
    : IQuery<PlanningIntervalObjectiveHealthCheckDetailsDto?>;

/// <summary>Handles <see cref="GetPlanningIntervalObjectiveHealthCheckQuery"/>.</summary>
public sealed class GetPlanningIntervalObjectiveHealthCheckQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetPlanningIntervalObjectiveHealthCheckQuery, PlanningIntervalObjectiveHealthCheckDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    /// <summary>Returns the check, or <c>null</c> when the ids do not line up.</summary>
    public Task<PlanningIntervalObjectiveHealthCheckDetailsDto?> Handle(GetPlanningIntervalObjectiveHealthCheckQuery request, CancellationToken cancellationToken)
    {
        var objectiveIdsInInterval = _planningDbContext.PlanningIntervalObjectives
            .Where(o => o.PlanningIntervalId == request.PlanningIntervalId)
            .Select(o => o.Id);

        return _planningDbContext.PlanningIntervalObjectiveHealthChecks
            .AsNoTracking()
            .Where(h => h.Id == request.HealthCheckId
                        && h.PlanningIntervalObjectiveId == request.PlanningIntervalObjectiveId
                        && objectiveIdsInInterval.Contains(h.PlanningIntervalObjectiveId))
            .ProjectToType<PlanningIntervalObjectiveHealthCheckDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
