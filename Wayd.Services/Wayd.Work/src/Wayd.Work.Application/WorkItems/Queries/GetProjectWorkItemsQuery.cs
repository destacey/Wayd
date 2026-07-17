using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

// TODO: change this to IdOrKey parameter
public sealed record GetProjectWorkItemsQuery(Guid ProjectId) : IQuery<Result<List<WorkItemListDto>>>;

public sealed class GetProjectWorkItemsQueryHandler(IWorkDbContext workDbContext)
    : IQueryHandler<GetProjectWorkItemsQuery, Result<List<WorkItemListDto>>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<Result<List<WorkItemListDto>>> Handle(GetProjectWorkItemsQuery request, CancellationToken cancellationToken)
    {
        return await _workDbContext.WorkItems
            .Where(i => (i.ProjectId != null && i.ProjectId == request.ProjectId)
                || (i.ProjectId == null && i.ParentProjectId != null && i.ParentProjectId == request.ProjectId))
            .ProjectToType<WorkItemListDto>()
            .ToListAsync(cancellationToken);
    }
}
