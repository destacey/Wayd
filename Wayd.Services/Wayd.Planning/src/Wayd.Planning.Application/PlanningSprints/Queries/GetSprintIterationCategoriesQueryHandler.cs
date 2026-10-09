using Wayd.Common.Application.Requests.Planning.Queries;
using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Planning.Application.PlanningSprints.Queries;

public sealed class GetSprintIterationCategoriesQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetSprintIterationCategoriesQuery, IReadOnlyDictionary<Guid, IterationCategory>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<IReadOnlyDictionary<Guid, IterationCategory>> Handle(GetSprintIterationCategoriesQuery request, CancellationToken cancellationToken)
    {
        if (request.SprintIds.Count == 0)
            return new Dictionary<Guid, IterationCategory>();

        // A soft-deleted PI keeps its mapping rows; the query filter leaves its navigation null.
        var mappings = await _planningDbContext.PlanningIntervalIterationSprints
            .Where(s => request.SprintIds.Contains(s.SprintId)
                && s.PlanningIntervalIteration.PlanningInterval != null)
            .Select(s => new { s.SprintId, s.PlanningIntervalIteration.Category })
            .ToListAsync(cancellationToken);

        return mappings.ToDictionary(m => m.SprintId, m => m.Category);
    }
}
