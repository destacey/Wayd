using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.Iterations.Dtos;

namespace Wayd.Planning.Application.Iterations.Queries;

public sealed record GetSprintsQuery(Guid? TeamId = null) : IQuery<List<SprintListDto>>;

public sealed class GetSprintsQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetSprintsQuery, List<SprintListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<List<SprintListDto>> Handle(GetSprintsQuery request, CancellationToken cancellationToken)
    {
        var query = _planningDbContext.Iterations
            .Where(i => i.Type == IterationType.Sprint && i.TeamId != null)
            .AsQueryable();

        if (request.TeamId.HasValue)
        {
            query = query.Where(i => i.TeamId == request.TeamId.Value);
        }

        var sprints = await query
            .ProjectToType<SprintListDto>()
            .ToListAsync(cancellationToken);

        return sprints;
    }
}
