using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums.Planning;
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

public sealed class GetPlanningIntervalIterationSprintsQueryHandler(IPlanningDbContext planningDbContext, IDispatcher dispatcher)
    : IQueryHandler<GetPlanningIntervalIterationSprintsQuery, List<PlanningIntervalIterationSprintsDto>?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;

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

        var sprints = iterations.SelectMany(i => i.Sprints).ToList();
        var states = await _dispatcher.Send(new GetIterationStatesQuery([.. sprints.Select(s => s.Id).Distinct()]), cancellationToken);
        foreach (var sprint in sprints)
        {
            var state = states.GetValueOrDefault(sprint.Id);
            sprint.State = SimpleNavigationDto.FromEnum(state?.State ?? IterationState.Unknown);
            sprint.ActiveFrom = state?.ActiveFrom;
            sprint.ActiveUntil = state?.ActiveUntil;
            sprint.TimeZone = state?.TimeZone;
        }

        return iterations;
    }
}
