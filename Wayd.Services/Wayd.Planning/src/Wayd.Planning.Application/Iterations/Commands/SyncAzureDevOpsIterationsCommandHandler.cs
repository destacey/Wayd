using Wayd.Common.Application.Events;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.Planning.Iterations;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Planning.Domain.Models.Iterations;

namespace Wayd.Planning.Application.Iterations.Commands;

public sealed class SyncAzureDevOpsIterationsCommandHandler(IPlanningDbContext planningDbContext, ILogger<SyncAzureDevOpsIterationsCommandHandler> logger, IDateTimeProvider dateTimeProvider, IEventPublisher eventPublisher)
 : ICommandHandler<SyncAzureDevOpsIterationsCommand>
{
    private const string AppRequestName = nameof(SyncAzureDevOpsIterationsCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<SyncAzureDevOpsIterationsCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IEventPublisher _eventPublisher = eventPublisher;

    public async Task<Result> Handle(SyncAzureDevOpsIterationsCommand request, CancellationToken cancellationToken)
    {
        if (request.Iterations.Count == 0)
        {
            _logger.LogInformation("No iterations to sync for SystemId {SystemId}.", request.SystemId);
            return Result.Success();
        }

        var syncLog = new IterationSyncLog(request.Iterations.Count);

        try
        {
            // Group incoming iterations by ProjectId and process each project separately (chunked)
            var groups = request.Iterations.GroupBy(i => i.Metadata.ProjectId);

            foreach (var group in groups)
            {
                var azdoProjectId = group.Key;

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Processing {IterationCount} iterations for SystemId {SystemId} and Azdo Project {AzdoProjectId}", group.Count(), request.SystemId, azdoProjectId);
                }

                var projectIterations = await _planningDbContext.Iterations
                .Include(i => i.ExternalMetadata)
                .Where(i => i.OwnershipInfo.SystemId == request.SystemId
                && i.ExternalMetadata.Any(em => em.Name == "ProjectId" && em.Value == azdoProjectId.ToString()))
                .ToListAsync(cancellationToken);

                // Delete iterations that are no longer present for this project. Don't stop processing if there is a delete error.
                var externalIdsToKeep = group.Select(i => i.Id.ToString()).ToHashSet(StringComparer.Ordinal);
                await DeleteMissingIterations(projectIterations, externalIdsToKeep, syncLog, cancellationToken);

                var processResult = await ProcessIterations(request.SystemId, group, azdoProjectId, projectIterations, request.TeamMappings, syncLog, cancellationToken);
                if (processResult.IsFailure)
                {
                    return PublishSyncLog(processResult, request.SystemId, azdoProjectId, syncLog);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for SystemId {SystemId}.", AppRequestName, request.SystemId);

            return PublishSyncLog(Result.Failure($"Error handling {AppRequestName} command."), request.SystemId, null, syncLog);
        }

        // All groups processed successfully
        return PublishSyncLog(Result.Success(), request.SystemId, null, syncLog);

        Result PublishSyncLog(Result result, string systemId, Guid? azdoProjectId, IterationSyncLog log)
        {
            _logger.LogInformation("Synced {Processed} external iterations for system {SystemId}{AzdoProjectPart}. Requested: {Requested}, Created: {Created}, Updated: {Updated}, Deleted: {Deleted}, Last External Id: {LastExternalId}.",
            log.Processed,
            systemId,
            azdoProjectId.HasValue ? $" and Azdo Project {azdoProjectId}" : string.Empty,
            log.Requested,
            log.Created,
            log.Updated,
            log.Deleted,
            log.LastExternalId);

            return result;
        }
    }

    /// <summary>
    /// Deletes iterations that exist in the local database but are no longer present in the external system
    /// for the current project group.
    /// </summary>
    private async Task DeleteMissingIterations(List<Iteration> projectIterations, HashSet<string> externalIdsToKeep, IterationSyncLog syncLog, CancellationToken cancellationToken)
    {
        if (projectIterations.Count == 0)
            return;

        var iterationsToDelete = projectIterations
        .Where(pi => pi.OwnershipInfo.ExternalId != null && !externalIdsToKeep.Contains(pi.OwnershipInfo.ExternalId))
        .ToList();

        if (iterationsToDelete.Count == 0)
            return;

        _planningDbContext.Iterations.RemoveRange(iterationsToDelete);

        try
        {
            await _planningDbContext.SaveChangesAsync(cancellationToken);
            syncLog.IterationDeleted(iterationsToDelete.Count);
        }
        catch (Exception ex)
        {
            // Log and continue: delete failures should not stop processing of other project groups
            _logger.LogError(ex, "Failed to delete {Count} iterations during sync. Continuing processing.", iterationsToDelete.Count);
        }

        try
        {
            foreach (var iteration in iterationsToDelete)
            {
                var deleteEvent = new IterationDeletedEvent(iteration.Id, _dateTimeProvider.Now);
                await _eventPublisher.PublishAsync(deleteEvent);
            }
        }
        catch (Exception ex)
        {
            // Log and continue: delete failures should not stop processing of other project groups
            _logger.LogError(ex, "Exception while processing iteration delete events");
        }
    }

    /// <summary>
    /// Processes the iterations from the provided external iteration group, updating existing iterations or creating new ones as necessary.
    /// </summary>
    private async Task<Result> ProcessIterations(string systemId, IEnumerable<IExternalIteration<AzdoIterationMetadata>> externalIterations, Guid azdoProjectId, List<Iteration> projectIterations, Dictionary<Guid, Guid?> teamMappings, IterationSyncLog syncLog, CancellationToken cancellationToken)
    {
        // Build lookups to avoid O(n^2) FirstOrDefault calls
        var existingByExternalId = projectIterations
        .Where(pi => !string.IsNullOrEmpty(pi.OwnershipInfo.ExternalId))
        .ToDictionary(pi => pi.OwnershipInfo.ExternalId!, StringComparer.Ordinal);

        var teamTypes = await _planningDbContext.PlanningTeams
            .Select(t => new { t.Id, t.Type })
            .ToDictionaryAsync(t => t.Id, t => t.Type, cancellationToken);

        foreach (var externalIteration in externalIterations)
        {
            syncLog.IterationRequested(externalIteration.Id);

            Guid? teamId = null;
            if (externalIteration.TeamId.HasValue)
            {
                teamMappings.TryGetValue(externalIteration.TeamId.Value, out teamId);
            }

            var sprintType = externalIteration.Type;
            if (sprintType == IterationType.Sprint)
            {
                // Validate that the mapped team (if any) supports sprints
                if (teamId.HasValue && teamTypes.TryGetValue(teamId.Value, out var teamType))
                {
                    if (teamType != TeamType.Team)
                    {
                        sprintType = IterationType.Iteration;
                    }
                }
            }

            var externalIdString = externalIteration.Id.ToString();
            if (existingByExternalId.TryGetValue(externalIdString, out var existingIteration))
            {
                var updateResult = existingIteration.Update(
                    externalIteration.Name,
                    sprintType,
                    externalIteration.State,
                    IterationDateRange.Create(externalIteration.Start, externalIteration.End),
                    teamId,
                    _dateTimeProvider.Now
                );
                if (updateResult.IsFailure)
                {
                    _logger.LogError("Failed to update iteration {IterationName} for SystemId {SystemId} and Azdo Project {AzdoProjectId}: {Error}", externalIteration.Name, systemId, azdoProjectId, updateResult.Error);
                    return Result.Failure($"Failed to update iterations for SystemId {systemId} and Azdo Project {azdoProjectId}.");
                }

                // Update editable external metadata
                existingIteration.ExternalMetadataManager.Upsert("Path", externalIteration.Metadata.Path);

                syncLog.IterationUpdated();
            }
            else
            {
                var ownershipInfo = OwnershipInfo.CreateExternalOwned(Connector.AzureDevOps, systemId, externalIdString);

                List<KeyValueObjectMetadata> externalMetadata =
                [
                    new(Guid.Empty, "ProjectId", azdoProjectId.ToString()),
                    new(Guid.Empty, "Identifier", externalIteration.Metadata.Identifier.ToString()),
                    new(Guid.Empty, "Path", externalIteration.Metadata.Path)
                ];

                var newIteration = Iteration.Create(
                    externalIteration.Name,
                    sprintType,
                    externalIteration.State,
                    IterationDateRange.Create(externalIteration.Start, externalIteration.End),
                    teamId,
                    ownershipInfo,
                    externalMetadata,
                    _dateTimeProvider.Now
                );

                await _planningDbContext.Iterations.AddAsync(newIteration, cancellationToken);

                syncLog.IterationCreated();
            }
        }

        // Persist changes for this project group (creates/updates)
        try
        {
            await _planningDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save iteration changes for SystemId {SystemId} and Azdo Project {AzdoProjectId}.", systemId, azdoProjectId);
            return Result.Failure($"Failed to save iteration changes for SystemId {systemId} and Azdo Project {azdoProjectId}.");
        }

        return Result.Success();
    }

    private sealed record IterationSyncLog
    {
        public IterationSyncLog(int requested)
        {
            Requested = requested;
        }

        public int Requested { get; init; }
        public int Created { get; private set; }
        public int Updated { get; private set; }
        public int Deleted { get; private set; }
        public int Processed => Created + Updated;
        public int? LastExternalId { get; private set; }

        public void IterationRequested(int externalId)
        {
            LastExternalId = externalId;
        }

        public void IterationCreated()
        {
            Created++;
            LastExternalId = null;
        }

        public void IterationUpdated()
        {
            Updated++;
            LastExternalId = null;
        }

        public void IterationDeleted(int count)
        {
            Deleted = count;
        }
    }
}
