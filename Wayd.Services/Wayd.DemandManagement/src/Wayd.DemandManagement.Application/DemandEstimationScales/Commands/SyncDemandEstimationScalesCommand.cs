using Microsoft.Extensions.Logging;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.Planning.EstimationScales;
using Wayd.DemandManagement.Domain.Models.Estimation;

namespace Wayd.DemandManagement.Application.DemandEstimationScales.Commands;

/// <summary>
/// Brings every <see cref="DemandEstimationScale"/> copy in line with Planning: the Estimation Scales Sync job's
/// repair for copies that missed an event.
/// </summary>
/// <param name="Scales">Every Planning estimation scale, with its versions, as read from the source.</param>
/// <param name="AsOf">When the source was read, taken before the read began.</param>
public sealed record SyncDemandEstimationScalesCommand(IEnumerable<EstimationScaleSnapshot> Scales, Instant AsOf) : ICommand, ILongRunningRequest;

/// <summary>
/// Creates missing copies, resyncs the rest, and removes the copies of deleted scales. A deleted scale a desk or an
/// estimate still uses keeps its copy, inactive, for as long as anything uses it, which is for good when the estimate
/// is on a closed request.
/// </summary>
public sealed class SyncDemandEstimationScalesCommandHandler(
    IDemandManagementDbContext dbContext,
    ILogger<SyncDemandEstimationScalesCommandHandler> logger)
    : ICommandHandler<SyncDemandEstimationScalesCommand>
{
    /// <summary>
    /// How long before the read a copy's last change must be for its absence from the read to mean its scale was
    /// deleted. An event's timestamp is taken before Planning commits, so a scale created just before the read can be
    /// missing from it while its copy, stamped earlier than the read, already exists.
    /// </summary>
    private static readonly Duration RecentChangeGrace = Duration.FromMinutes(5);

    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ILogger<SyncDemandEstimationScalesCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncDemandEstimationScalesCommand request, CancellationToken cancellationToken)
    {
        var sources = request.Scales.ToList();
        var existing = await _dbContext.DemandEstimationScales.Include(s => s.Versions).ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(s => s.Id);
        var sourceIds = sources.Select(s => s.Id).ToHashSet();

        var deleted = 0;
        var kept = 0;
        // A copy that took a change after, or just before, the read may belong to a scale the read could not see yet.
        var deletedBefore = request.AsOf - RecentChangeGrace;
        foreach (var gone in existing.Where(s => !sourceIds.Contains(s.Id) && !s.Watermarks.AnyAfter(deletedBefore)))
        {
            if (await _dbContext.IsInUse(gone.Id, cancellationToken))
            {
                if (gone.IsActive)
                    gone.ApplyIsActive(false, request.AsOf);
                kept++;
            }
            else
            {
                _dbContext.DemandEstimationScales.Remove(gone);
                deleted++;
            }
        }

        var created = 0;
        var updated = 0;
        foreach (var source in sources)
        {
            if (!existingById.TryGetValue(source.Id, out var copy))
            {
                await _dbContext.DemandEstimationScales.AddAsync(new DemandEstimationScale(source, request.AsOf), cancellationToken);
                created++;
            }
            else if (copy.Resync(source, request.AsOf))
            {
                updated++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Demand Management {SystemActionType} for {CreateCount} created, {UpdateCount} updated, {DeleteCount} deleted, and {KeptCount} deleted but still used estimation scales.",
            SystemActionType.ServiceDataReplication, created, updated, deleted, kept);

        return Result.Success();
    }
}
