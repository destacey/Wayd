using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Enums;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Workspaces.Commands;

/// <summary>
/// Clears a managed workspace's need for a full sync once one has run.
/// </summary>
/// <remarks>
/// A work item the sync skipped, because its state is in no workflow its type follows, still has no engine status
/// afterwards. Keeping the need for them would make every later sync a full one that cannot help, so the need is
/// cleared and the items are logged for someone to look into.
/// </remarks>
public sealed class CompleteWorkspaceFullSyncCommandHandler(
    IWorkDbContext workDbContext,
    ILogger<CompleteWorkspaceFullSyncCommandHandler> logger)
    : ICommandHandler<CompleteWorkspaceFullSyncCommand>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<CompleteWorkspaceFullSyncCommandHandler> _logger = logger;

    /// <summary>Clears the workspace's need for a full sync, and logs any work item still without a status.</summary>
    public async Task<Result> Handle(CompleteWorkspaceFullSyncCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _workDbContext.Workspaces
            .FirstOrDefaultAsync(w => w.Id == request.WorkspaceId && w.OwnershipInfo.Ownership == Ownership.Managed, cancellationToken);
        if (workspace is null)
            return Result.Failure($"Managed workspace {request.WorkspaceId} does not exist.");

        if (!workspace.NeedsFullSync)
            return Result.Success();

        var withoutStatus = await _workDbContext.WorkItems
            .CountAsync(w => w.WorkspaceId == workspace.Id && w.StatusId == Guid.Empty, cancellationToken);
        if (withoutStatus > 0)
        {
            _logger.LogWarning(
                "Workspace {WorkspaceId} completed its full sync with {Count} work items whose state is in no workflow their type follows; they keep their last known status name and category.",
                workspace.Id, withoutStatus);
        }

        workspace.FullSyncCompleted();
        await _workDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
