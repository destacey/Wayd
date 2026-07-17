using System.Linq.Expressions;
using Wayd.Common.Application.Models;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

/// <summary>
/// Query to check if a planning interval objective exists. If it exists, returns the ID of the objective.
/// </summary>
public sealed record CheckPlanningIntervalObjectiveExistsQuery : IQuery<Guid?>
{
    public CheckPlanningIntervalObjectiveExistsQuery(IdOrKey idOrKey, IdOrKey objectiveIdOrKey)
    {
        PlanningIntervalIdOrKeyFilter = idOrKey.CreateFilter<PlanningInterval>();
        ObjectiveIdOrKeyFilter = objectiveIdOrKey.CreateFilter<PlanningIntervalObjective>();
    }

    public Expression<Func<PlanningInterval, bool>> PlanningIntervalIdOrKeyFilter { get; }
    public Expression<Func<PlanningIntervalObjective, bool>> ObjectiveIdOrKeyFilter { get; }
}

public sealed class CheckPlanningIntervalObjectiveExistsQueryHandler(IPlanningDbContext planningDbContext) : IQueryHandler<CheckPlanningIntervalObjectiveExistsQuery, Guid?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<Guid?> Handle(CheckPlanningIntervalObjectiveExistsQuery request, CancellationToken cancellationToken)
    {
        return await _planningDbContext.PlanningIntervals
            .Where(request.PlanningIntervalIdOrKeyFilter)
            .SelectMany(p => p.Objectives)
            .Where(request.ObjectiveIdOrKeyFilter)
            .Select(o => (Guid?)o.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
