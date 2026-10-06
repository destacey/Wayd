using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItems.Commands;

public sealed class ResetWorkItemHistoryCommandHandler(IWorkDbContext workDbContext, ILogger<ResetWorkItemHistoryCommandHandler> logger) : ICommandHandler<ResetWorkItemHistoryCommand>
{
    // Each delete stays well inside the command timeout however large the workspace's history is.
    private const int DeleteBatchSize = 10_000;

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<ResetWorkItemHistoryCommandHandler> _logger = logger;

    public async Task<Result> Handle(ResetWorkItemHistoryCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _workDbContext.Workspaces
            .FirstOrDefaultAsync(w => w.Id == request.WorkspaceId, cancellationToken);
        if (workspace is null)
            return Result.Failure($"Unable to reset the work item history of workspace {request.WorkspaceId} because the workspace does not exist.");

        // One transaction, so a failure part way leaves the history and its watermark as they were
        // rather than a partly emptied history the watermark says is complete.
        await using var unitOfWork = await _workDbContext.BeginUnitOfWork(cancellationToken);

        var deleted = 0;
        int batch;
        do
        {
            batch = await _workDbContext.WorkItemStateHistory
                .Where(h => h.WorkspaceId == workspace.Id)
                .OrderBy(h => h.Id)
                .Take(DeleteBatchSize)
                .ExecuteDeleteAsync(cancellationToken);
            deleted += batch;
        } while (batch == DeleteBatchSize);

        workspace.SetWorkItemHistoryWatermark(null);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        await unitOfWork.CommitAsync(cancellationToken);

        _logger.LogInformation("Reset the work item history of workspace {WorkspaceId}, deleting {Count} periods.", workspace.Id, deleted);

        return Result.Success();
    }
}
