using Wayd.Common.Application.Dtos;

namespace Wayd.Planning.Application.Iterations.Queries;

public sealed record GetSprintPlanningIntervalsQuery(int SprintKey) : IQuery<IReadOnlyList<NavigationDto>>;

public sealed class GetSprintPlanningIntervalsQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetSprintPlanningIntervalsQuery, IReadOnlyList<NavigationDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<IReadOnlyList<NavigationDto>> Handle(GetSprintPlanningIntervalsQuery request, CancellationToken cancellationToken)
    {
        return await _planningDbContext.PlanningIntervalIterationSprints
            .Where(s => s.Sprint.Key == request.SprintKey
                && s.PlanningIntervalIteration.PlanningInterval != null)
            .Select(s => new NavigationDto
            {
                Id = s.PlanningIntervalIteration.PlanningInterval!.Id,
                Key = s.PlanningIntervalIteration.PlanningInterval!.Key,
                Name = s.PlanningIntervalIteration.PlanningInterval!.Name,
            })
            .Distinct()
            .OrderBy(pi => pi.Name)
            .ToListAsync(cancellationToken);
    }
}
