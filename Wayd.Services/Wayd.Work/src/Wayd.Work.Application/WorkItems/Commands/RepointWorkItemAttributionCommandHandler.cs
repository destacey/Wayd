using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItems.Commands;

/// <summary>
/// Applies an admin's identity decision to work already synced.
/// </summary>
/// <remarks>
/// Repairs all three attributions the sync resolves — assignee, author, and last-modifier — so a
/// mapping fixes the whole record rather than the half a user happens to look at first.
/// <para>
/// Set-based rather than loading aggregates: a single identity can own tens of thousands of work
/// items, and every column written here is a denormalized attribution pointer that carries no
/// domain invariant of its own — the invariant lives on the mapping row this is driven from.
/// </para>
/// <para>
/// Only items whose sync recorded an external identity id can be reached. Items synced before
/// those columns existed have no pointer, so a full sync has to run before a mapping can repoint
/// them.
/// </para>
/// </remarks>
public sealed class RepointWorkItemAttributionCommandHandler(
    IWorkDbContext workDbContext,
    ILogger<RepointWorkItemAttributionCommandHandler> logger)
    : ICommandHandler<RepointWorkItemAttributionCommand>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<RepointWorkItemAttributionCommandHandler> _logger = logger;

    public async Task<Result> Handle(RepointWorkItemAttributionCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalId))
            return Result.Failure("An external identity id is required to repoint work item attribution.");

        var externalId = request.ExternalId.Trim();

        try
        {
            // All three attributions the sync resolves, so a mapping repairs the record rather
            // than half of it. Each is its own set-based statement: they match different rows
            // (one person may have created an item someone else is assigned), and each column
            // has its own index to seek on.
            var assignedUpdated = await _workDbContext.WorkItems
                .Where(wi => wi.ExtendedProps != null
                    && wi.ExtendedProps.AssignedToExternalId == externalId
                    && wi.AssignedToId != request.EmployeeId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(wi => wi.AssignedToId, request.EmployeeId),
                    cancellationToken);

            var createdUpdated = await _workDbContext.WorkItems
                .Where(wi => wi.ExtendedProps != null
                    && wi.ExtendedProps.CreatedByExternalId == externalId
                    && wi.CreatedById != request.EmployeeId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(wi => wi.CreatedById, request.EmployeeId),
                    cancellationToken);

            var lastModifiedUpdated = await _workDbContext.WorkItems
                .Where(wi => wi.ExtendedProps != null
                    && wi.ExtendedProps.LastModifiedByExternalId == externalId
                    && wi.LastModifiedById != request.EmployeeId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(wi => wi.LastModifiedById, request.EmployeeId),
                    cancellationToken);

            if (assignedUpdated + createdUpdated + lastModifiedUpdated > 0)
            {
                _logger.LogInformation(
                    "Repointed external identity {ExternalId} to employee {EmployeeId}: {AssignedCount} assigned, {CreatedCount} created, {LastModifiedCount} last-modified.",
                    externalId, request.EmployeeId, assignedUpdated, createdUpdated, lastModifiedUpdated);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error repointing work item attribution for external identity {ExternalId}.", externalId);
            return Result.Failure(ex.Message);
        }
    }
}
