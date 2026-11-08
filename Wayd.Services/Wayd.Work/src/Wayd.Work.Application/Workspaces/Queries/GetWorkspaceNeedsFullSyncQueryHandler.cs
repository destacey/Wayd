using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Workspaces.Queries;

/// <summary>
/// Reads whether a managed workspace's next sync must be a full one.
/// </summary>
public sealed class GetWorkspaceNeedsFullSyncQueryHandler(IWorkDbContext workDbContext) : IQueryHandler<GetWorkspaceNeedsFullSyncQuery, Result<bool>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<Result<bool>> Handle(GetWorkspaceNeedsFullSyncQuery request, CancellationToken cancellationToken)
    {
        var needsFullSync = await _workDbContext.Workspaces
            .Where(w => w.Id == request.WorkspaceId && w.OwnershipInfo.Ownership == Ownership.Managed)
            .Select(w => (bool?)w.NeedsFullSync)
            .FirstOrDefaultAsync(cancellationToken);

        return needsFullSync is null
            ? Result.Failure<bool>($"Managed workspace {request.WorkspaceId} does not exist.")
            : Result.Success(needsFullSync.Value);
    }
}
