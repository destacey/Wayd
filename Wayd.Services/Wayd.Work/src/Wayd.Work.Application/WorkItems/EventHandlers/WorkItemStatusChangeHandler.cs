using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Domain;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.Application.WorkItems.EventHandlers;

/// <summary>
/// Brings work items and their history in step with a status of their workflow that changed: renamed by its
/// source, reclassified by its source, or given another alias.
/// </summary>
/// <remarks>
/// A work item stores a copy of its status's name, category and alias (<see cref="ManagedStatusEntity"/>), and a
/// managed workflow is published from the start, so each of these changes reaches items that already hold the
/// status. A history period keeps the name its source recorded, but takes the status's new category, so reports
/// that read history agree with the workflow.
/// <para>
/// Both are written set-based, guarded on the status itself: the item sync of the same run may be moving items off
/// the status while this runs, and a guarded update can neither stamp an item that has moved with the old status's
/// state nor skip one, as loading and saving items in pages could. <see cref="WorkItem"/> and its periods raise no
/// domain events, so nothing is bypassed.
/// </para>
/// <para>
/// Delivered durably (<c>DurableEventRoutes</c>), in a scope of its own, so a failure is retried rather than lost
/// once the workflow's change has committed. Each handler re-reads the status rather than trusting the event, so
/// a late or repeated delivery re-applies the current state, which changes nothing the second time. Every
/// workflow's change arrives here, and one whose workflow is not a work item's is ignored.
/// </para>
/// <para>
/// Retiring a status, or reinstating it, needs no handler: items keep the status and its name and category, and
/// the one thing retiring changes that an item stores, its alias, arrives as its own
/// <see cref="WorkflowStatusAliasChangedEvent"/>.
/// </para>
/// </remarks>
public sealed class WorkItemStatusChangeHandler(
    IWorkDbContext workDbContext,
    IStatusWorkflowDbContext statusWorkflowDbContext,
    ILogger<WorkItemStatusChangeHandler> logger)
{
    /// <summary>
    /// How long each update may run. A status can be held by tens of thousands of items, each row carried by
    /// several covering indexes, which the request ceiling is not sized for.
    /// </summary>
    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromMinutes(5);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IStatusWorkflowDbContext _statusWorkflowDbContext = statusWorkflowDbContext;
    private readonly ILogger<WorkItemStatusChangeHandler> _logger = logger;

    /// <summary>Re-applies a work item status's new category to the items and periods holding it.</summary>
    public Task Handle(WorkflowStatusReclassifiedEvent @event, CancellationToken cancellationToken) =>
        Reapply(@event.WorkflowId, @event.StatusId, cancellationToken);

    /// <summary>Re-applies a status's new name to the items holding it; periods keep the name their source recorded.</summary>
    public Task Handle(WorkflowStatusRenamedEvent @event, CancellationToken cancellationToken) =>
        Reapply(@event.Id, @event.StatusId, cancellationToken);

    /// <summary>Re-applies a status's new alias to the items holding it.</summary>
    public Task Handle(WorkflowStatusAliasChangedEvent @event, CancellationToken cancellationToken) =>
        Reapply(@event.Id, @event.StatusId, cancellationToken);

    private async Task Reapply(Guid workflowId, Guid statusId, CancellationToken cancellationToken)
    {
        var status = await _statusWorkflowDbContext.StatusWorkflows
            .Where(w => w.Id == workflowId && w.OwnerType == WorkWorkflowOwners.WorkItem.Key)
            .SelectMany(w => w.Statuses)
            .Where(s => s.Id == statusId)
            .FirstOrDefaultAsync(cancellationToken);
        if (status is null)
            return;

        var current = StatusRef.From(status);

        using var timeout = _workDbContext.WithCommandTimeout(UpdateTimeout);

        var items = await ReapplyToItems(current, cancellationToken);
        var periods = await ReapplyToHistory(current, cancellationToken);

        if (items > 0 || periods > 0)
        {
            _logger.LogInformation(
                "Re-applied status {StatusId} of workflow {WorkflowId} to {ItemCount} work items and {PeriodCount} history periods.",
                statusId, workflowId, items, periods);
        }
    }

    /// <summary>
    /// Re-applies the status's name, category and alias to every item holding it, and clears the done date of
    /// those whose status no longer counts as finished (<see cref="WorkItem.CountsAsFinished"/>). A done date is
    /// never invented: the source reports it with the item, so it returns with the item's next sync.
    /// </summary>
    private Task<int> ReapplyToItems(StatusRef status, CancellationToken cancellationToken)
    {
        var finished = WorkItem.CountsAsFinished(status.Category);

        return _workDbContext.WorkItems
            .Where(w => w.StatusWorkflowId == status.WorkflowId && w.StatusId == status.StatusId)
            .ExecuteUpdateAsync(set =>
            {
                set.SetProperty(w => w.StatusName, status.Name);
                set.SetProperty(w => w.StatusCategory, status.Category);
                set.SetProperty(w => w.StatusAliasValue, status.Alias);
                if (!finished)
                    set.SetProperty(w => w.DoneTimestamp, (Instant?)null);
            }, cancellationToken);
    }

    /// <summary>
    /// Re-stamps the category on every history period holding the status.
    /// </summary>
    private Task<int> ReapplyToHistory(StatusRef status, CancellationToken cancellationToken) =>
        _workDbContext.WorkItemStateHistory
            .Where(h => h.StatusId == status.StatusId && h.StatusCategory != status.Category)
            .ExecuteUpdateAsync(set => set.SetProperty(h => h.StatusCategory, status.Category), cancellationToken);
}
