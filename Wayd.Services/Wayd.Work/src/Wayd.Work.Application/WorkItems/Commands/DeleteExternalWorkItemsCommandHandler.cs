using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Enums;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItems.Commands;

/// <summary>
/// Deletes external work items, with their links, from the managed workspaces the request names.
/// </summary>
public sealed class DeleteExternalWorkItemsCommandHandler(IWorkDbContext workDbContext, ILogger<DeleteExternalWorkItemsCommandHandler> logger) : ICommandHandler<DeleteExternalWorkItemsCommand, int>
{
    // Keeps each IN list well under SQL Server's parameter limit; a full sync can delete thousands.
    private const int BatchSize = 1000;

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<DeleteExternalWorkItemsCommandHandler> _logger = logger;

    public async Task<Result<int>> Handle(DeleteExternalWorkItemsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.WorkItemIds.Length == 0 || request.WorkspaceIds.Count == 0)
            {
                return 0;
            }

            var requestedWorkspaceIds = request.WorkspaceIds.Distinct().ToArray();
            var workspaceIds = await _workDbContext.Workspaces
                .Where(w => requestedWorkspaceIds.Contains(w.Id) && w.OwnershipInfo.Ownership == Ownership.Managed)
                .Select(w => w.Id)
                .ToArrayAsync(cancellationToken);
            if (workspaceIds.Length == 0)
            {
                _logger.LogWarning("Unable to delete external work items because none of workspaces {WorkspaceIds} exist.", requestedWorkspaceIds);
                return Result.Failure<int>($"Unable to delete external work items because none of workspaces {string.Join(", ", requestedWorkspaceIds)} exist.");
            }

            var deleted = 0;
            foreach (var batch in request.WorkItemIds.Distinct().Chunk(BatchSize))
            {
                var workItems = await _workDbContext.WorkItems
                    .Include(w => w.Children)
                    .Include(w => w.OutboundDependencies)
                    .Include(w => w.InboundDependencies)
                    .Include(w => w.OutboundHierarchies)
                    .Include(w => w.InboundHierarchies)
                    .Include(w => w.ReferenceLinks)
                    .Where(w => workspaceIds.Contains(w.WorkspaceId) && w.ExternalId != null && batch.Contains(w.ExternalId.Value))
                    .ToListAsync(cancellationToken);

                if (workItems.Count == 0)
                    continue;

                _workDbContext.WorkItemDependencies.RemoveRange(workItems.SelectMany(w => w.OutboundDependencies.Concat(w.InboundDependencies)));
                _workDbContext.WorkItemHierarchies.RemoveRange(workItems.SelectMany(w => w.OutboundHierarchies.Concat(w.InboundHierarchies)));
                _workDbContext.WorkItems.RemoveRange(workItems);

                await _workDbContext.SaveChangesAsync(cancellationToken);
                deleted += workItems.Count;
            }

            _logger.LogInformation("{WorkItemCount} of {RequestedCount} external work items deleted from workspaces {WorkspaceIds}.", deleted, request.WorkItemIds.Length, workspaceIds);

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown deleting external work items from workspaces {WorkspaceIds}.", request.WorkspaceIds);
            throw;
        }
    }
}
