using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

public sealed record GetPlanningIntervalIterationQuery : IQuery<PlanningIntervalIterationDetailsDto?>
{
    public GetPlanningIntervalIterationQuery(IdOrKey planningIntervalIdOrKey, IdOrKey iterationIdOrKey)
    {
        PlanningIntervalIdOrKeyFilter = planningIntervalIdOrKey.CreateFilter<PlanningInterval>();
        IterationIdOrKeyFilter = iterationIdOrKey.CreateFilter<PlanningIntervalIteration>();
    }

    public Expression<Func<PlanningInterval, bool>> PlanningIntervalIdOrKeyFilter { get; }
    public Expression<Func<PlanningIntervalIteration, bool>> IterationIdOrKeyFilter { get; }
}

public sealed class GetPlanningIntervalIterationQueryHandler(IPlanningDbContext planningDbContext, ISystemCalendar systemCalendar)
    : IQueryHandler<GetPlanningIntervalIterationQuery, PlanningIntervalIterationDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ISystemCalendar _systemCalendar = systemCalendar;

    public async Task<PlanningIntervalIterationDetailsDto?> Handle(GetPlanningIntervalIterationQuery request, CancellationToken cancellationToken)
    {
        LocalDate today = await _systemCalendar.Today(cancellationToken);
        var config = PlanningIntervalIterationDetailsDto.CreateTypeAdapterConfig(today);

        var iteration = await _planningDbContext.PlanningIntervals
            .Where(request.PlanningIntervalIdOrKeyFilter)
            .SelectMany(pi => pi.Iterations)
            .Where(request.IterationIdOrKeyFilter)
            .ProjectToType<PlanningIntervalIterationDetailsDto>(config)
            .FirstOrDefaultAsync(cancellationToken);

        return iteration;
    }
}