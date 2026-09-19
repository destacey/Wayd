namespace Wayd.ProductManagement.Application;

/// <summary>
/// Stages the status history of records being deleted for removal. The caller saves.
/// </summary>
/// <remarks>
/// The history table serves every status-tracked type and has no foreign key to any of them, so nothing
/// cascades to it: left behind, those rows would still surface in the delivery overview. The two
/// contexts are views over one, so the caller's single save removes the history with the records.
/// </remarks>
internal static class StatusHistoryRemoval
{
    public static async Task Stage(
        IStatusWorkflowDbContext statusWorkflowDbContext,
        string ownerType,
        IReadOnlyCollection<Guid> recordIds,
        CancellationToken cancellationToken)
    {
        if (recordIds.Count == 0)
        {
            return;
        }

        var transitions = await statusWorkflowDbContext.StatusTransitions
            .Where(t => t.OwnerType == ownerType && recordIds.Contains(t.RecordId))
            .ToListAsync(cancellationToken);

        statusWorkflowDbContext.StatusTransitions.RemoveRange(transitions);
    }
}
