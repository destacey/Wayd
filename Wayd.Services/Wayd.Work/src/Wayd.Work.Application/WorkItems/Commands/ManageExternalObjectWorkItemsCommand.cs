using Wayd.Common.Domain.Enums;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItems.Commands;

public sealed record ManageExternalObjectWorkItemsCommand(Guid PlanningIntervalId, Guid ObjectiveId, SystemContext Context, IEnumerable<Guid> WorkItemIds) : ICommand;

public sealed class ManageExternalObjectWorkItemsCommandHandler(IWorkDbContext workDbContext, ILogger<ManageExternalObjectWorkItemsCommandHandler> logger) : ICommandHandler<ManageExternalObjectWorkItemsCommand>
{
    private const string AppRequestName = nameof(ManageExternalObjectWorkItemsCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<ManageExternalObjectWorkItemsCommandHandler> _logger = logger;

    public async Task<Result> Handle(ManageExternalObjectWorkItemsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var workItemLinks = await _workDbContext.WorkItemReferences
                .Where(w => w.ObjectId == request.ObjectiveId)
                .ToListAsync(cancellationToken);

            var existingWorkItemIds = workItemLinks.Select(x => x.WorkItemId).ToHashSet();
            var newWorkItemIds = request.WorkItemIds.ToHashSet();

            var workItemIdsToRemove = existingWorkItemIds.Except(newWorkItemIds).ToArray();
            _logger.LogDebug("Removing {WorkItemsToRemoveCount} from Objective {ObjectiveId}.", workItemIdsToRemove.Length, request.ObjectiveId);
            if (workItemIdsToRemove.Length > 0)
            {
                var workItemLinksToRemove = workItemLinks.Where(x => workItemIdsToRemove.Contains(x.WorkItemId)).ToArray();
                _workDbContext.WorkItemReferences.RemoveRange(workItemLinksToRemove);
            }

            var workItemIdsToAdd = newWorkItemIds.Except(existingWorkItemIds).ToArray();
            _logger.LogDebug("Adding {WorkItemsToAddCount} to Objective {ObjectiveId}.", workItemIdsToAdd.Length, request.ObjectiveId);
            if (workItemIdsToAdd.Length > 0)
            {
                var workItemLinksToAdd = workItemIdsToAdd.Select(x => WorkItemReference.Create(x, request.ObjectiveId, request.Context)).ToArray();
                await _workDbContext.WorkItemReferences.AddRangeAsync(workItemLinksToAdd, cancellationToken);
            }

            await _workDbContext.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Successfully managed work items for Objective {ObjectiveId}.", request.ObjectiveId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
