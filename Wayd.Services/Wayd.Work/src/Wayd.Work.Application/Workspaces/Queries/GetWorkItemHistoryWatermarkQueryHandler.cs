using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Workspaces.Queries;

/// <summary>
/// Reads where a workspace's work item history sync left off, so a differential sync resumes there.
/// </summary>
public sealed class GetWorkItemHistoryWatermarkQueryHandler(IWorkDbContext workDbContext) : IQueryHandler<GetWorkItemHistoryWatermarkQuery, Result<WorkItemHistoryCursor>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<Result<WorkItemHistoryCursor>> Handle(GetWorkItemHistoryWatermarkQuery request, CancellationToken cancellationToken)
    {
        var cursor = await _workDbContext.Workspaces
            .Where(w => w.Id == request.WorkspaceId && w.OwnershipInfo.Ownership == Ownership.Managed)
            .Select(w => new WorkItemHistoryCursor(w.WorkItemHistoryWatermark, w.WorkItemHistoryBackfilledOn != null))
            .FirstOrDefaultAsync(cancellationToken);

        return cursor is null
            ? Result.Failure<WorkItemHistoryCursor>($"Managed workspace {request.WorkspaceId} does not exist.")
            : Result.Success(cursor);
    }
}
