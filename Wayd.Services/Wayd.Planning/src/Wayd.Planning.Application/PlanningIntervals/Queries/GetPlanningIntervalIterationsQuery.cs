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

public sealed class GetPlanningIntervalIterationsQueryHandler(IPlanningDbContext planningDbContext, IDateTimeProvider dateTimeProvider) : IQueryHandler<GetPlanningIntervalIterationsQuery, IReadOnlyList<PlanningIntervalIterationListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<IReadOnlyList<PlanningIntervalIterationListDto>> Handle(GetPlanningIntervalIterationsQuery request, CancellationToken cancellationToken)
    {
        LocalDate today = _dateTimeProvider.Today;
        var config = PlanningIntervalIterationListDto.CreateTypeAdapterConfig(today);

        var iterations = await _planningDbContext.PlanningIntervals
            .Where(request.IdOrKeyFilter)
            .SelectMany(p => p.Iterations)
            .ProjectToType<PlanningIntervalIterationListDto>(config)
            .ToListAsync(cancellationToken);

        return [.. iterations.OrderBy(i => i.Start)];
    }
}
