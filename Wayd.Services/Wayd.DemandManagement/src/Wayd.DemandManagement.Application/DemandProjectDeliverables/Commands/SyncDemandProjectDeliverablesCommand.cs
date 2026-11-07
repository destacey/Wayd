using Microsoft.Extensions.Logging;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.DemandProjectDeliverables.Commands;

/// <summary>
/// Brings every <see cref="DemandProjectDeliverable"/> copy in line with PPM: the Projects Sync job's repair
/// for copies that missed an event. Runs after the project copies are synced, since a deliverable's copy
/// needs its project's.
/// </summary>
/// <param name="Deliverables">Every PPM project deliverable, as read from the source.</param>
/// <param name="AsOf">When the source was read, taken before the read began.</param>
public sealed record SyncDemandProjectDeliverablesCommand(IEnumerable<ISimpleProjectDeliverable> Deliverables, Instant AsOf)
    : ICommand, ILongRunningRequest;

public sealed class SyncDemandProjectDeliverablesCommandHandler(
    IDemandManagementDbContext dbContext,
    ILogger<SyncDemandProjectDeliverablesCommandHandler> logger)
    : ICommandHandler<SyncDemandProjectDeliverablesCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ILogger<SyncDemandProjectDeliverablesCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncDemandProjectDeliverablesCommand request, CancellationToken cancellationToken)
    {
        var sources = request.Deliverables.ToList();
        var existing = await _dbContext.DemandProjectDeliverables.ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(d => d.Id);
        var sourceIds = sources.Select(d => d.Id).ToHashSet();
        var projectIds = (await _dbContext.DemandProjects.Select(p => p.Id).ToListAsync(cancellationToken)).ToHashSet();

        // A copy that took a change after the read belongs to a deliverable added after it, not a removed one.
        var removed = existing
            .Where(d => !sourceIds.Contains(d.Id) && !d.Watermarks.AnyAfter(request.AsOf))
            .ToList();
        _dbContext.DemandProjectDeliverables.RemoveRange(removed);

        var created = 0;
        var updated = 0;
        var skipped = 0;
        foreach (var source in sources)
        {
            if (!existingById.TryGetValue(source.Id, out var copy))
            {
                // The project copy was deleted or not yet made; its own events or the next run settle it.
                if (!projectIds.Contains(source.ProjectId))
                {
                    skipped++;
                    continue;
                }

                await _dbContext.DemandProjectDeliverables.AddAsync(new DemandProjectDeliverable(source, request.AsOf), cancellationToken);
                created++;
            }
            else if (copy.Resync(source, request.AsOf))
            {
                updated++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Demand Management {SystemActionType} for {CreateCount} created, {UpdateCount} updated, {DeleteCount} deleted, and {SkipCount} skipped project deliverables.",
            SystemActionType.ServiceDataReplication, created, updated, removed.Count, skipped);

        return Result.Success();
    }
}
