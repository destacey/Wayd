using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Commands;

public sealed class SyncExternalWorkItemParentChangesCommandHandler(IWorkDbContext workDbContext, ILogger<SyncExternalWorkItemParentChangesCommandHandler> logger) : ICommandHandler<SyncExternalWorkItemParentChangesCommand>
{
    private const string AppRequestName = nameof(SyncExternalWorkItemParentChangesCommand);

    private static readonly Dictionary<string, int> _changedOperationOrder = new()
    {
        { "create", 1 },
        { "remove", 2 }
    };

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<SyncExternalWorkItemParentChangesCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncExternalWorkItemParentChangesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // get parent work items
            var parentIds = request.WorkItemLinks
                .Where(wil => wil.ChangedOperation == "create")
                .Select(wil => wil.SourceId)
                .Distinct().ToList();

            var parentWorkItems = await _workDbContext.WorkItems
                .Where(wi => wi.ExternalId.HasValue && parentIds.Contains(wi.ExternalId.Value))
                .ProjectToType<WorkItemParentInfo>()
                .ToListAsync(cancellationToken);

            // get distinct child: full work item
            var childrenIds = request.WorkItemLinks.Select(wil => wil.TargetId).Distinct().ToArray();
            var childrenWorkItems = await _workDbContext.WorkItems
                .Include(wi => wi.Type)
                    .ThenInclude(t => t.Level)
                .Where(wi => wi.WorkspaceId == request.WorkspaceId && wi.ExternalId.HasValue && childrenIds.Contains(wi.ExternalId.Value))
                .ToListAsync(cancellationToken);

            // loop through the children and process the latest parent change
            foreach (var child in childrenWorkItems)
            {
                var parentLink = request.WorkItemLinks
                    .Where(wil => wil.TargetId == child.ExternalId!.Value)
                    .OrderByDescending(wil => wil.ChangedDate)
                        .ThenBy(wil => _changedOperationOrder.TryGetValue(wil.ChangedOperation, out int value) ? value : int.MaxValue)
                    .First();

                var parent = parentLink.ChangedOperation == "remove"
                    ? null
                    : parentWorkItems.FirstOrDefault(pwi => pwi.ExternalId == parentLink.SourceId);  // TODO: make sure the workspace id matches

                var updateParentResult = child.UpdateParent(parent, child.Type);
                if (updateParentResult.IsFailure)
                {
                    _logger.LogWarning("Error updating parent for work item {WorkItemId} (ExternalId: {ExternalId}): {Error}", child.Id, child.ExternalId, updateParentResult.Error);
                }
                else if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Updated parent for work item {WorkItemId} (ExternalId: {ExternalId})", child.Id, child.ExternalId);
                }
            }

            await _workDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown handling {AppRequestName}", AppRequestName);
            return Result.Failure(ex.ToString());
        }
    }
}
