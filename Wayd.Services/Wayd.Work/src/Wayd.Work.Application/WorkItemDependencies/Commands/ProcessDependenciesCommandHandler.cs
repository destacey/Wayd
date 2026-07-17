using System.Diagnostics;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItemDependencies.Commands;

public sealed class ProcessDependenciesCommandHandler(
    IWorkDbContext workDbContext,
    ILogger<ProcessDependenciesCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ProcessDependenciesCommand>
{
    private const string AppRequestName = nameof(ProcessDependenciesCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<ProcessDependenciesCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ProcessDependenciesCommand request, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var startTime = Stopwatch.GetTimestamp();

        // Build a queryable for dependencies filtered by SystemId
        var dependenciesQuery = _workDbContext.WorkItemDependencies
            .Where(d => d.Source!.Workspace.OwnershipInfo.SystemId == request.SystemId);

        // Load dependencies that need to be updated
        var dependencies = await dependenciesQuery.ToListAsync(cancellationToken);
        if (dependencies.Count == 0)
        {
            return Result.Success();
        }

        // Collect all unique work item IDs (both sources and targets) from dependencies
        var workItemIds = dependencies
            .SelectMany(d => new[] { d.SourceId, d.TargetId })
            .Distinct()
            .ToArray();

        // Get all unique work items and project to DependencyWorkItemInfo using Contains for better performance
        var workItemInfoDictionary = await _workDbContext.WorkItems
            .Where(wi => workItemIds.Contains(wi.Id))
            .Select(DependencyWorkItemInfo.Projection)
            .ToDictionaryAsync(x => x.WorkItemId, cancellationToken);

        if (workItemInfoDictionary.Count == 0)
        {
            _logger.LogError("No work item info found for dependencies in SystemId: {SystemId}", request.SystemId);
            return Result.Failure("No work item info found for dependencies.");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("{AppRequestName}: Processing {DependencyCount} dependencies for SystemId: {SystemId}", AppRequestName, dependencies.Count, request.SystemId);
            _logger.LogDebug("{AppRequestName}: Found {WorkItemCount} work items for SystemId: {SystemId}", AppRequestName, workItemInfoDictionary.Count, request.SystemId);
        }

        int modifiedCount = 0;
        foreach (var dependency in dependencies)
        {
            if (workItemInfoDictionary.TryGetValue(dependency.SourceId, out var sourceInfo) &&
                workItemInfoDictionary.TryGetValue(dependency.TargetId, out var targetInfo))
            {
                dependency.UpdateSourceAndTargetInfo(sourceInfo, targetInfo, now);

                // Check if EF detected any changes
                var entry = _workDbContext.Entry(dependency);
                if (entry.State == EntityState.Modified)
                {
                    modifiedCount++;
                }
            }
            else
            {
                _logger.LogWarning("Missing work item info for dependency {DependencyId}. Source: {SourceId}, Target: {TargetId}",
                    dependency.Id, dependency.SourceId, dependency.TargetId);
            }
        }

        await _workDbContext.SaveChangesAsync(cancellationToken);

        var elapsedMilliseconds = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;
        _logger.LogInformation("Completed processing {DependencyCount} dependencies for SystemId: {SystemId}. Modified: {ModifiedCount}. Elapsed: {ElapsedMilliseconds} ms",
            dependencies.Count, request.SystemId, modifiedCount, elapsedMilliseconds);

        return Result.Success();
    }
}
