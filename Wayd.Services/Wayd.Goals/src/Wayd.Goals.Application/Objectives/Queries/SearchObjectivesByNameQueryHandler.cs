using Wayd.Common.Application.Requests.Goals.Dtos;
using Wayd.Common.Application.Requests.Goals.Queries;
using Wayd.Goals.Application.Persistence;

namespace Wayd.Goals.Application.Objectives.Queries;

internal sealed class SearchObjectivesByNameQueryHandler(IGoalsDbContext goalsDbContext)
    : IQueryHandler<SearchObjectivesByNameQuery, IReadOnlyList<ObjectiveSearchResultDto>>
{
    public async Task<IReadOnlyList<ObjectiveSearchResultDto>> Handle(SearchObjectivesByNameQuery request, CancellationToken cancellationToken)
    {
        return await goalsDbContext.Objectives
            .Where(o => o.PlanId.HasValue && o.Name.Contains(request.SearchTerm))
            .OrderBy(o => o.Name)
            .Select(o => new ObjectiveSearchResultDto
            {
                Id = o.Id,
                Name = o.Name,
                PlanId = o.PlanId,
                OwnerId = o.OwnerId
            })
            .Take(request.MaxResults)
            .ToListAsync(cancellationToken);
    }
}
