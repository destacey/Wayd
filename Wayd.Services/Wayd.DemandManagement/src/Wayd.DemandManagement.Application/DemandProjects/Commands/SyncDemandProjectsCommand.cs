using Microsoft.Extensions.Logging;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.DemandProjects.Commands;

/// <summary>
/// Brings every <see cref="DemandProject"/> copy in line with PPM: the Projects Sync job's repair for copies
/// that missed an event, and how projects that predate the copy get one.
/// </summary>
/// <param name="Projects">Every PPM project, as read from the source.</param>
/// <param name="AsOf">When the source was read, taken before the read began.</param>
public sealed record SyncDemandProjectsCommand(IEnumerable<ISimpleProject> Projects, Instant AsOf) : ICommand, ILongRunningRequest;

public sealed class SyncDemandProjectsCommandHandler(
    IDemandManagementDbContext dbContext,
    ILogger<SyncDemandProjectsCommandHandler> logger)
    : ICommandHandler<SyncDemandProjectsCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ILogger<SyncDemandProjectsCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncDemandProjectsCommand request, CancellationToken cancellationToken)
    {
        var sources = request.Projects.ToList();
        var existing = await _dbContext.DemandProjects.ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(p => p.Id);
        var sourceIds = sources.Select(p => p.Id).ToHashSet();

        // A copy that took a change after the read belongs to a project created after it, not a deleted one.
        var deleted = existing
            .Where(p => !sourceIds.Contains(p.Id) && !p.Watermarks.AnyAfter(request.AsOf))
            .ToList();
        _dbContext.DemandProjects.RemoveRange(deleted);

        var created = 0;
        var updated = 0;
        foreach (var source in sources)
        {
            if (!existingById.TryGetValue(source.Id, out var copy))
            {
                await _dbContext.DemandProjects.AddAsync(new DemandProject(source, request.AsOf), cancellationToken);
                created++;
            }
            else if (copy.Resync(source, request.AsOf))
            {
                updated++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Demand Management {SystemActionType} for {CreateCount} created, {UpdateCount} updated, and {DeleteCount} deleted projects.",
            SystemActionType.ServiceDataReplication, created, updated, deleted.Count);

        return Result.Success();
    }
}
