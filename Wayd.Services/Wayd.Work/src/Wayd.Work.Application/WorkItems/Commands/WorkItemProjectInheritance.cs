using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Commands;

/// <summary>
/// Passes a Portfolio-tier work item's project down to the work items beneath it that inherit it.
/// </summary>
internal static class WorkItemProjectInheritance
{
    /// <summary>
    /// Points each child of <paramref name="parent"/> at the parent's current project, saving after each
    /// level, then descends into the children that hold no project of their own. The parent's change must
    /// already be saved, and its type and level loaded. A child the domain refuses to re-parent (a synced
    /// hierarchy whose levels are out of order) is logged and skipped rather than failing the change.
    /// </summary>
    internal static async Task PushToDescendants(IWorkDbContext workDbContext, WorkItem parent, ILogger logger, CancellationToken cancellationToken)
    {
        if (parent.Type.Level!.Tier != WorkTypeTier.Portfolio)
            return;

        var children = await workDbContext.WorkItems
            .Include(wi => wi.Type)
                .ThenInclude(t => t.Level)
            .Where(wi => wi.ParentId == parent.Id)
            .ToListAsync(cancellationToken);

        if (children.Count == 0)
            return;

        var parentInfo = ToParentInfo(parent);
        int updateCount = 0;

        foreach (var child in children)
        {
            var result = child.UpdateParent(parentInfo, child.Type);
            if (result.IsFailure)
            {
                logger.LogError("Error updating parent for work item {WorkItemId}. {Error}", child.Id, result.Error);
                continue;
            }

            updateCount++;
        }

        if (updateCount > 0)
        {
            await workDbContext.SaveChangesAsync(cancellationToken);
            logger.LogDebug("Updated {UpdateCount} children for work item {WorkItemId}.", updateCount, parent.Id);
        }

        foreach (var child in children)
        {
            if (child.ProjectId is null)
                await PushToDescendants(workDbContext, child, logger, cancellationToken);
        }
    }

    private static WorkItemParentInfo ToParentInfo(WorkItem workItem) => new()
    {
        Id = workItem.Id,
        ExternalId = workItem.ExternalId,
        Tier = workItem.Type.Level!.Tier,
        LevelOrder = workItem.Type.Level.Order,
        ProjectId = workItem.CurrentProjectId,
    };
}
