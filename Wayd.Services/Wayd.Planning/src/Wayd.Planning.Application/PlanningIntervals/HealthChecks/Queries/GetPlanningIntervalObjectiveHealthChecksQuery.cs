using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.HealthChecks.Queries;

/// <summary>
/// An objective's health check history, newest first, or <c>null</c> when the objective is not in the given
/// planning interval.
/// </summary>
public sealed record GetPlanningIntervalObjectiveHealthChecksQuery(Guid PlanningIntervalId, Guid PlanningIntervalObjectiveId)
    : IQuery<IReadOnlyList<PlanningIntervalObjectiveHealthCheckDetailsDto>?>;

/// <summary>Handles <see cref="GetPlanningIntervalObjectiveHealthChecksQuery"/>.</summary>
public sealed class GetPlanningIntervalObjectiveHealthChecksQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetPlanningIntervalObjectiveHealthChecksQuery, IReadOnlyList<PlanningIntervalObjectiveHealthCheckDetailsDto>?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    /// <summary>Returns the history, or <c>null</c> when the objective is not in the planning interval.</summary>
    public async Task<IReadOnlyList<PlanningIntervalObjectiveHealthCheckDetailsDto>?> Handle(GetPlanningIntervalObjectiveHealthChecksQuery request, CancellationToken cancellationToken)
    {
        var objectiveInInterval = await _planningDbContext.PlanningIntervalObjectives
            .AnyAsync(o => o.Id == request.PlanningIntervalObjectiveId && o.PlanningIntervalId == request.PlanningIntervalId, cancellationToken);
        if (!objectiveInInterval)
            return null;

        var healthChecks = await _planningDbContext.PlanningIntervalObjectiveHealthChecks
            .AsNoTracking()
            .Where(h => h.PlanningIntervalObjectiveId == request.PlanningIntervalObjectiveId)
            .OrderByDescending(h => h.ReportedOn)
            // Id last, so an MCP caller's offset slices the same list on every call.
            .ThenBy(h => h.Id)
            .ProjectToType<PlanningIntervalObjectiveHealthCheckDetailsDto>()
            .ToListAsync(cancellationToken);

        return healthChecks;
    }
}
