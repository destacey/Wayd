using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Workspaces.Queries;

/// <summary>
/// Reads where a workspace's work item history sync left off, so a differential sync resumes there.
/// </summary>
public sealed class GetWorkItemHistoryWatermarkQueryHandler(IWorkDbContext workDbContext) : IQueryHandler<GetWorkItemHistoryWatermarkQuery, Result<string?>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<Result<string?>> Handle(GetWorkItemHistoryWatermarkQuery request, CancellationToken cancellationToken)
    {
        var workspace = await _workDbContext.Workspaces
            .Where(w => w.Id == request.WorkspaceId)
            .Select(w => new { w.WorkItemHistoryWatermark })
            .FirstOrDefaultAsync(cancellationToken);

        return workspace is null
            ? Result.Failure<string?>($"Workspace {request.WorkspaceId} does not exist.")
            : Result.Success(workspace.WorkItemHistoryWatermark);
    }
}
