using Wayd.Common.Domain.Enums.Planning;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Queries;

public sealed record GetSprintsQuery(Guid? TeamId = null) : IQuery<List<SprintListDto>>;

public sealed class GetSprintsQueryHandler(IWorkDbContext workDbContext)
    : IQueryHandler<GetSprintsQuery, List<SprintListDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<List<SprintListDto>> Handle(GetSprintsQuery request, CancellationToken cancellationToken)
    {
        var query = _workDbContext.Iterations
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
