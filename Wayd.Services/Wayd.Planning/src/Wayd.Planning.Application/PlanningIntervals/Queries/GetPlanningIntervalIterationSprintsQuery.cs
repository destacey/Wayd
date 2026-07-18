using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

/// <summary>
/// Query to get iteration sprint mappings for a Planning Interval.
/// </summary>
/// <param name="IterationId">Optional iteration ID to filter mappings.</param>
public sealed record GetPlanningIntervalIterationSprintsQuery : IQuery<List<PlanningIntervalIterationSprintsDto>?>
{
    public GetPlanningIntervalIterationSprintsQuery(string idOrKey, Guid? iterationId = null)
    {
        IdOrKeyFilter = new IdOrKey(idOrKey).CreateFilter<PlanningInterval>();
        IterationId = iterationId;
    }

    public Expression<Func<PlanningInterval, bool>> IdOrKeyFilter { get; }
    public Guid? IterationId { get; }
}

public sealed class GetPlanningIntervalIterationSprintsQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetPlanningIntervalIterationSprintsQuery, List<PlanningIntervalIterationSprintsDto>?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<List<PlanningIntervalIterationSprintsDto>?> Handle(GetPlanningIntervalIterationSprintsQuery request, CancellationToken cancellationToken)
    {
        var query = _planningDbContext.PlanningIntervals
            .Where(request.IdOrKeyFilter)
            .SelectMany(pi => pi.Iterations);

        // Get iterations (filtered if requested)
        if (request.IterationId.HasValue)
        {
            query = query.Where(it => it.Id == request.IterationId.Value);
        }

        var iterations = await query
            .ProjectToType<PlanningIntervalIterationSprintsDto>()
            .ToListAsync(cancellationToken);

        return iterations;
    }
}
