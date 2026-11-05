using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>Lists the external ids of the work items in the requested workspaces; items Wayd created have none.</summary>
public sealed class GetExternalWorkItemIdsQueryHandler(IWorkDbContext workDbContext) : IQueryHandler<GetExternalWorkItemIdsQuery, Result<int[]>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<Result<int[]>> Handle(GetExternalWorkItemIdsQuery request, CancellationToken cancellationToken)
    {
        if (request.WorkspaceIds.Count == 0)
            return Result.Success<int[]>([]);

        var workspaceIds = request.WorkspaceIds.Distinct().ToArray();
        var ids = await _workDbContext.WorkItems
            .Where(w => workspaceIds.Contains(w.WorkspaceId) && w.ExternalId != null)
            .Select(w => w.ExternalId!.Value)
            .ToArrayAsync(cancellationToken);

        return Result.Success(ids);
    }
}
