namespace Wayd.Planning.Application.PlanningIntervals.Commands;

public sealed record ManagePlanningIntervalTeamsCommand(Guid Id, IEnumerable<Guid> TeamIds) : ICommand;

public sealed class ManagePlanningIntervalTeamsCommandHandler : ICommandHandler<ManagePlanningIntervalTeamsCommand>
{
    private const string AppRequestName = nameof(ManagePlanningIntervalTeamsCommand);

    private readonly IPlanningDbContext _planningDbContext;
    private readonly ILogger<ManagePlanningIntervalTeamsCommandHandler> _logger;

    public ManagePlanningIntervalTeamsCommandHandler(IPlanningDbContext planningDbContext, ILogger<ManagePlanningIntervalTeamsCommandHandler> logger)
    {
        _planningDbContext = planningDbContext;
        _logger = logger;
    }

    public async Task<Result> Handle(ManagePlanningIntervalTeamsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var planningInterval = await _planningDbContext.PlanningIntervals
                .Include(x => x.Teams)
                .Include(x => x.IterationSprints)
                    .ThenInclude(itsp => itsp.Sprint)
                .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

            if (planningInterval == null)
            {
                _logger.LogWarning("Planning Interval {PlanningIntervalId} not found.", request.Id);
                return Result.Failure<int>($"Planning Interval {request.Id} not found.");
            }

            var requestedTeamIds = request.TeamIds?.Distinct().ToList() ?? [];

            // Validate every requested team has a replicated PlanningTeam projection. PlanningIntervalTeam.TeamId
            // is a required cascade FK to PlanningTeam, and Team replication is delivered asynchronously, so a
            // team just created in Organization may not have landed here yet. Guard with a clean failure rather
            // than letting the SaveChanges below FK-fault.
            if (requestedTeamIds.Count != 0)
            {
                var existingTeamIds = await _planningDbContext.PlanningTeams
                    .Where(t => requestedTeamIds.Contains(t.Id))
                    .Select(t => t.Id)
                    .ToListAsync(cancellationToken);

                var missingTeamIds = requestedTeamIds.Except(existingTeamIds).ToList();
                if (missingTeamIds.Count != 0)
                {
                    _logger.LogWarning("Planning Interval {PlanningIntervalId} team assignment rejected: {MissingCount} team(s) not found in Planning: {MissingTeamIds}.", request.Id, missingTeamIds.Count, string.Join(", ", missingTeamIds));
                    return Result.Failure($"One or more teams could not be found. They may still be syncing — please try again in a moment.");
                }
            }

            var result = planningInterval.ManageTeams(requestedTeamIds);
            if (result.IsFailure)
                return Result.Failure(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}